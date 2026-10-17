using System;
using LuckyTrash.Cards;
using NUnit.Framework;

namespace LuckyTrash.UI.Tests
{
    public class DiscardTargetsTests
    {
        private static readonly Card S1 = new Card(Suit.Spade, 1);
        private static readonly Card H5 = new Card(Suit.Heart, 5);
        private static readonly Card D5 = new Card(Suit.Diamond, 5);
        private static readonly Card C9 = new Card(Suit.Club, 9);
        private static readonly Card H12 = new Card(Suit.Heart, 12);
        private static readonly Card S13 = new Card(Suit.Spade, 13);

        [Test]
        public void GetIndices_SingleDiscard_ReturnsItsPosition()
        {
            CollectionAssert.AreEqual(new[] { 2 }, DiscardTargets.GetIndices(new[] { S1, H5, D5, C9 }, new[] { D5 }));
        }

        [Test]
        public void GetIndices_MultipleDiscards_ReturnsPositionsInAscendingOrder()
        {
            // 捨てられた順（判定の順）に関わらず、手札の左から順に返す。
            CollectionAssert.AreEqual(new[] { 1, 4 },
                DiscardTargets.GetIndices(new[] { S1, H5, D5, C9, H12, S13 }, new[] { H12, H5 }));
        }

        [Test]
        public void GetIndices_AllCardsDiscarded_ReturnsEveryPosition()
        {
            CollectionAssert.AreEqual(new[] { 0, 1 }, DiscardTargets.GetIndices(new[] { H5, D5 }, new[] { D5, H5 }));
        }

        [Test]
        public void GetIndices_NoDiscard_ReturnsEmpty()
        {
            CollectionAssert.IsEmpty(DiscardTargets.GetIndices(new[] { S1, H5 }, new Card[0]));
        }

        [Test]
        public void GetIndices_DiscardNotInHand_IsIgnored()
        {
            CollectionAssert.AreEqual(new[] { 0 }, DiscardTargets.GetIndices(new[] { S1, H5 }, new[] { S1, C9 }));
        }

        [Test]
        public void GetIndices_NullArgument_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => DiscardTargets.GetIndices(null, new Card[0]));
            Assert.Throws<ArgumentNullException>(() => DiscardTargets.GetIndices(new Card[0], null));
        }
    }
}
