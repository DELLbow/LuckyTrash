using System;
using System.Collections.Generic;
using LuckyTrash.Cards;
using NUnit.Framework;

namespace LuckyTrash.Game.Tests
{
    public class PlayerTests
    {
        [Test]
        public void NewPlayer_HasEmptyHand_NullRank_AndIsEliminated()
        {
            var player = new Player(0);

            Assert.AreEqual(0, player.Hand.Count);
            Assert.IsNull(player.Rank);
            Assert.IsTrue(player.IsEliminated, "手札0枚はあがった状態(IsEliminated)とみなす。");
        }

        [Test]
        public void AddToHand_SingleCard_IncreasesHandCount()
        {
            var player = new Player(0);

            player.AddToHand(new Card(Suit.Spade, 1));

            Assert.AreEqual(1, player.Hand.Count);
            Assert.IsFalse(player.IsEliminated);
        }

        [Test]
        public void AddToHand_MultipleCards_AddsAll()
        {
            var player = new Player(0);
            var cards = new List<Card>
            {
                new Card(Suit.Spade, 1),
                new Card(Suit.Heart, 2),
                new Card(Suit.Club, 3)
            };

            player.AddToHand(cards);

            Assert.AreEqual(3, player.Hand.Count);
        }

        [Test]
        public void RemoveFromHand_RemovesOnlySpecifiedCards()
        {
            var player = new Player(0);
            var keep = new Card(Suit.Diamond, 9);
            var discard1 = new Card(Suit.Spade, 7);
            var discard2 = new Card(Suit.Heart, 7);
            player.AddToHand(new[] { keep, discard1, discard2 });

            player.RemoveFromHand(new[] { discard1, discard2 });

            Assert.AreEqual(1, player.Hand.Count);
            Assert.AreEqual(keep, player.Hand[0]);
        }

        [Test]
        public void RemoveFromHand_AllCards_MakesPlayerEliminated()
        {
            var player = new Player(0);
            var cards = new List<Card> { new Card(Suit.Club, 5) };
            player.AddToHand(cards);

            player.RemoveFromHand(cards);

            Assert.AreEqual(0, player.Hand.Count);
            Assert.IsTrue(player.IsEliminated);
        }

        [Test]
        public void ConfirmRank_SetsRank()
        {
            var player = new Player(0);

            player.ConfirmRank(1);

            Assert.AreEqual(1, player.Rank);
        }

        [Test]
        public void Constructor_NegativeSeatIndex_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Player(-1));
        }
    }
}
