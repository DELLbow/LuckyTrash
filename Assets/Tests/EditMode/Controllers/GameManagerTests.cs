using LuckyTrash.Controllers;
using NUnit.Framework;
using UnityEngine;

namespace LuckyTrash.Controllers.Tests
{
    public class GameManagerTests
    {
        private GameObject _go;
        private GameManager _gameManager;

        [SetUp]
        public void SetUp()
        {
            // GameManager.Instance（静的シングルトン）は使わず、テストごとに独立したインスタンスを
            // 直接生成する。DontDestroyOnLoad はエディタのシーン境界の影響を受けないため、
            // Awake が走っても問題なくテスト後に破棄できる。
            _go = new GameObject("GameManagerTestInstance");
            _gameManager = _go.AddComponent<GameManager>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                Object.DestroyImmediate(_go);
            }
        }

        [Test]
        public void TryGetStartingSeat_NoRecordYet_ReturnsFalse()
        {
            bool result = _gameManager.TryGetStartingSeat(4, new[] { 0, 1, 2, 3 }, out int startingSeat);

            Assert.IsFalse(result);
        }

        [Test]
        public void TryGetStartingSeat_SamePlayerCountAndSeats_ReturnsTrue_WithRecordedLastPlaceSeat()
        {
            // 「同じ人数・同じ座席構成で2回目、前回最下位だった座席から開始する」ことの検証。
            _gameManager.RecordGameResult(4, new[] { 0, 1, 2, 3 }, lastPlaceSeatIndex: 2);

            bool result = _gameManager.TryGetStartingSeat(4, new[] { 0, 1, 2, 3 }, out int startingSeat);

            Assert.IsTrue(result);
            Assert.AreEqual(2, startingSeat);
        }

        [Test]
        public void TryGetStartingSeat_DifferentPlayerCount_ReturnsFalse()
        {
            _gameManager.RecordGameResult(4, new[] { 0, 1, 2, 3 }, lastPlaceSeatIndex: 2);

            bool result = _gameManager.TryGetStartingSeat(3, new[] { 0, 1, 2 }, out int startingSeat);

            Assert.IsFalse(result);
        }

        [Test]
        public void TryGetStartingSeat_DifferentSeatComposition_ReturnsFalse()
        {
            // 座席の「集合」比較ロジックを、人数の一致だけに引きずられずに検証する
            // （実際の GetSeatMapping では人数が同じなら座席集合も一意に決まるが、
            //  TryGetStartingSeat 自体は座席集合の完全一致を独立してチェックする設計になっている）。
            _gameManager.RecordGameResult(3, new[] { 0, 1, 2 }, lastPlaceSeatIndex: 1);

            bool result = _gameManager.TryGetStartingSeat(3, new[] { 0, 1, 3 }, out int startingSeat);

            Assert.IsFalse(result);
        }

        [Test]
        public void TryGetStartingSeat_SeatOrderDoesNotMatter()
        {
            _gameManager.RecordGameResult(4, new[] { 0, 1, 2, 3 }, lastPlaceSeatIndex: 3);

            bool result = _gameManager.TryGetStartingSeat(4, new[] { 3, 2, 1, 0 }, out int startingSeat);

            Assert.IsTrue(result);
            Assert.AreEqual(3, startingSeat);
        }

        [Test]
        public void TryGetStartingSeat_AfterFailedLookup_OutParamIsNegativeOne()
        {
            bool result = _gameManager.TryGetStartingSeat(2, new[] { 0, 1 }, out int startingSeat);

            Assert.IsFalse(result);
            Assert.AreEqual(-1, startingSeat);
        }
    }
}
