using NUnit.Framework;

namespace LuckyTrash.UI.Tests
{
    public class MiniTableSeatsTests
    {
        // 通常のカメラはZ+側から-Z方向を向くため、ワールドの右座席は画面の左、左座席は画面の右に見える。
        [TestCase(CameraDirector.SeatSide.Bottom, NameplateSeatDirection.Down)]
        [TestCase(CameraDirector.SeatSide.Right, NameplateSeatDirection.Left)]
        [TestCase(CameraDirector.SeatSide.Top, NameplateSeatDirection.Up)]
        [TestCase(CameraDirector.SeatSide.Left, NameplateSeatDirection.Right)]
        public void ToScreenPosition_MapsSeatToOnScreenPosition(CameraDirector.SeatSide side, NameplateSeatDirection expected)
        {
            Assert.AreEqual(expected, MiniTableSeats.ToScreenPosition(side));
        }

        [Test]
        public void GetOccupiedPositions_FourPlayers_ShowsAllFourSeats()
        {
            var positions = MiniTableSeats.GetOccupiedPositions(new[]
            {
                CameraDirector.SeatSide.Bottom, CameraDirector.SeatSide.Right,
                CameraDirector.SeatSide.Top, CameraDirector.SeatSide.Left
            });

            CollectionAssert.AreEquivalent(new[]
            {
                NameplateSeatDirection.Down, NameplateSeatDirection.Left,
                NameplateSeatDirection.Up, NameplateSeatDirection.Right
            }, positions);
        }

        [Test]
        public void GetOccupiedPositions_ThreePlayers_OmitsTopSeat()
        {
            var positions = MiniTableSeats.GetOccupiedPositions(new[]
            {
                CameraDirector.SeatSide.Bottom, CameraDirector.SeatSide.Right, CameraDirector.SeatSide.Left
            });

            CollectionAssert.AreEquivalent(new[]
            {
                NameplateSeatDirection.Down, NameplateSeatDirection.Left, NameplateSeatDirection.Right
            }, positions);
        }

        [Test]
        public void GetOccupiedPositions_TwoPlayers_OmitsLeftAndRightSeats()
        {
            var positions = MiniTableSeats.GetOccupiedPositions(new[]
            {
                CameraDirector.SeatSide.Bottom, CameraDirector.SeatSide.Top
            });

            CollectionAssert.AreEquivalent(new[] { NameplateSeatDirection.Down, NameplateSeatDirection.Up }, positions);
        }
    }
}
