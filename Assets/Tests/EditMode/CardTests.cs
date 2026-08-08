using System;
using LuckyTrash.Cards;
using NUnit.Framework;

namespace LuckyTrash.Cards.Tests
{
    public class CardTests
    {
        [Test]
        public void Color_HeartAndDiamond_AreRed()
        {
            Assert.AreEqual(CardColor.Red, new Card(Suit.Heart, 1).Color);
            Assert.AreEqual(CardColor.Red, new Card(Suit.Diamond, 13).Color);
        }

        [Test]
        public void Color_SpadeAndClub_AreBlack()
        {
            Assert.AreEqual(CardColor.Black, new Card(Suit.Spade, 1).Color);
            Assert.AreEqual(CardColor.Black, new Card(Suit.Club, 13).Color);
        }

        [TestCase(0)]
        [TestCase(14)]
        [TestCase(-1)]
        public void Constructor_RankOutOfRange_Throws(int rank)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Card(Suit.Spade, rank));
        }

        [Test]
        public void Equality_SameSuitAndRank_AreEqual()
        {
            var a = new Card(Suit.Club, 7);
            var b = new Card(Suit.Club, 7);

            Assert.AreEqual(a, b);
            Assert.IsTrue(a == b);
            Assert.IsFalse(a != b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void Equality_DifferentRank_AreNotEqual()
        {
            var a = new Card(Suit.Club, 7);
            var b = new Card(Suit.Club, 8);

            Assert.AreNotEqual(a, b);
            Assert.IsTrue(a != b);
        }
    }
}
