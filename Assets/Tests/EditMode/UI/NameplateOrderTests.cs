using System;
using NUnit.Framework;

namespace LuckyTrash.UI.Tests
{
    public class NameplateOrderTests
    {
        // 2人: SeatIndex 0=Bottom, 1=Top
        [TestCase(0, new[] { 0, 1 })]
        [TestCase(1, new[] { 1, 0 })]
        public void GetTurnOrder_TwoPlayers_StartsFromStartingSeatClockwise(int startingSeat, int[] expected)
        {
            CollectionAssert.AreEqual(expected, NameplateOrder.GetTurnOrder(new[] { 0, 1 }, startingSeat));
        }

        // 3人: SeatIndex 0=Bottom, 1=Right, 2=Left
        [TestCase(0, new[] { 0, 1, 2 })]
        [TestCase(1, new[] { 1, 2, 0 })]
        [TestCase(2, new[] { 2, 0, 1 })]
        public void GetTurnOrder_ThreePlayers_StartsFromStartingSeatClockwise(int startingSeat, int[] expected)
        {
            CollectionAssert.AreEqual(expected, NameplateOrder.GetTurnOrder(new[] { 0, 1, 2 }, startingSeat));
        }

        // 4人: SeatIndex 0=Bottom, 1=Right, 2=Top, 3=Left
        [TestCase(0, new[] { 0, 1, 2, 3 })]
        [TestCase(1, new[] { 1, 2, 3, 0 })]
        [TestCase(2, new[] { 2, 3, 0, 1 })]
        [TestCase(3, new[] { 3, 0, 1, 2 })]
        public void GetTurnOrder_FourPlayers_StartsFromStartingSeatClockwise(int startingSeat, int[] expected)
        {
            CollectionAssert.AreEqual(expected, NameplateOrder.GetTurnOrder(new[] { 0, 1, 2, 3 }, startingSeat));
        }

        [Test]
        public void GetTurnOrder_UnsortedInput_UsesAscendingSeatOrder()
        {
            CollectionAssert.AreEqual(new[] { 2, 3, 0, 1 }, NameplateOrder.GetTurnOrder(new[] { 3, 1, 0, 2 }, 2));
        }

        [Test]
        public void GetTurnOrder_StartingSeatNotInList_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => NameplateOrder.GetTurnOrder(new[] { 0, 1 }, 2));
        }
    }
}
