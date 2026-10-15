using LuckyTrash.Game;
using NUnit.Framework;
using UnityEngine;

namespace LuckyTrash.UI.Tests
{
    public class RouletteAngleMathTests
    {
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void GetRestAngleForSlice_ThenGetSliceIndexAtPointer_RoundTripsToSameIndex(int sliceIndex)
        {
            float restAngle = RouletteAngleMath.GetRestAngleForSlice(sliceIndex);
            int resolvedIndex = RouletteAngleMath.GetSliceIndexAtPointer(restAngle);

            Assert.AreEqual(sliceIndex, resolvedIndex,
                $"restAngle={restAngle}で停止した場合にポインター位置に来る扇形が、選ばれたインデックス{sliceIndex}と一致しません。");
        }

        [TestCase(0f, 3)]
        [TestCase(45f, 4)]
        [TestCase(359f, 5)]
        [TestCase(720.5f, 3)]
        [TestCase(-30f, 0)]
        public void ComputeTargetAngle_FromVariousStartAnglesAndTurns_LandsOnChosenSliceForAllIndices(float startAngle, int extraTurns)
        {
            for (int sliceIndex = 0; sliceIndex < 6; sliceIndex++)
            {
                float restAngle = RouletteAngleMath.GetRestAngleForSlice(sliceIndex);
                float targetAngle = RouletteAngleMath.ComputeTargetAngle(startAngle, restAngle, extraTurns);

                // Quaternion.Euler に渡される前提で、最終角度は 0-360 に正規化される。
                float finalAngle = Mathf.Repeat(targetAngle, 360f);
                int resolvedIndex = RouletteAngleMath.GetSliceIndexAtPointer(finalAngle);

                Assert.AreEqual(sliceIndex, resolvedIndex,
                    $"start={startAngle}, extraTurns={extraTurns}, sliceIndex={sliceIndex} で停止扇形が一致しません。");

                // 逆回転していないこと＝見た目の回転方向（常に正方向）を変えていないことの確認。
                Assert.GreaterOrEqual(targetAngle, startAngle);
            }
        }

        [Test]
        public void ComputeTargetAngle_RotatesAtLeastRequestedExtraTurns_AndLessThanOneMore()
        {
            float startAngle = 10f;
            float restAngle = RouletteAngleMath.GetRestAngleForSlice(2);
            const int extraTurns = 3;

            float targetAngle = RouletteAngleMath.ComputeTargetAngle(startAngle, restAngle, extraTurns);
            float totalRotation = targetAngle - startAngle;

            Assert.GreaterOrEqual(totalRotation, 360f * extraTurns);
            Assert.Less(totalRotation, 360f * (extraTurns + 1));
        }

        [Test]
        public void AllSixSliceIndices_StoppedCategoryAtPointer_MatchesRouletteSelectorAllFaces()
        {
            // SpinTo(resultCategory) が選ぶインデックスについて、停止後にポインターへ来る扇形のカテゴリが
            // 抽選結果（AllFaces[chosenSliceIndex]）と一致することを、6インデック全てで確認する。
            for (int chosenSliceIndex = 0; chosenSliceIndex < 6; chosenSliceIndex++)
            {
                float restAngle = RouletteAngleMath.GetRestAngleForSlice(chosenSliceIndex);
                int sliceIndexAtPointer = RouletteAngleMath.GetSliceIndexAtPointer(restAngle);

                RouletteCategory expectedCategory = RouletteSelector.AllFaces[chosenSliceIndex];
                RouletteCategory actualCategoryAtPointer = RouletteSelector.AllFaces[sliceIndexAtPointer];

                Assert.AreEqual(expectedCategory, actualCategoryAtPointer,
                    $"chosenSliceIndex={chosenSliceIndex} ({expectedCategory}) が選ばれた際、" +
                    $"停止後にポインターへ来る扇形はindex{sliceIndexAtPointer} ({actualCategoryAtPointer}) でした。");
            }
        }
    }
}
