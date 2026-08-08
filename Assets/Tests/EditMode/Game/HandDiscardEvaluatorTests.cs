using System;
using System.Collections.Generic;
using LuckyTrash.Cards;
using NUnit.Framework;

namespace LuckyTrash.Game.Tests
{
    public class HandDiscardEvaluatorTests
    {
        [Test]
        public void Number_MatchesSameRank_AcrossMultipleSuits()
        {
            var hand = new List<Card>
            {
                new Card(Suit.Spade, 7),
                new Card(Suit.Heart, 7),
                new Card(Suit.Diamond, 7),
                new Card(Suit.Club, 3), // 対象外
                new Card(Suit.Spade, 9) // 対象外
            };
            var reference = new Card(Suit.Club, 7);

            var result = HandDiscardEvaluator.Evaluate(hand, RouletteCategory.Number, reference);

            Assert.AreEqual(3, result.Count);
            CollectionAssert.Contains(result, new Card(Suit.Spade, 7));
            CollectionAssert.Contains(result, new Card(Suit.Heart, 7));
            CollectionAssert.Contains(result, new Card(Suit.Diamond, 7));
            CollectionAssert.DoesNotContain(result, new Card(Suit.Club, 3));
            CollectionAssert.DoesNotContain(result, new Card(Suit.Spade, 9));
        }

        [Test]
        public void Suit_MatchesSameSuit_AcrossMultipleRanks()
        {
            var hand = new List<Card>
            {
                new Card(Suit.Heart, 2),
                new Card(Suit.Heart, 10),
                new Card(Suit.Heart, 13),
                new Card(Suit.Spade, 2),   // 対象外
                new Card(Suit.Diamond, 10) // 対象外
            };
            var reference = new Card(Suit.Heart, 5);

            var result = HandDiscardEvaluator.Evaluate(hand, RouletteCategory.Suit, reference);

            Assert.AreEqual(3, result.Count);
            CollectionAssert.Contains(result, new Card(Suit.Heart, 2));
            CollectionAssert.Contains(result, new Card(Suit.Heart, 10));
            CollectionAssert.Contains(result, new Card(Suit.Heart, 13));
            CollectionAssert.DoesNotContain(result, new Card(Suit.Spade, 2));
            CollectionAssert.DoesNotContain(result, new Card(Suit.Diamond, 10));
        }

        [Test]
        public void Color_Red_MatchesHeartAndDiamond()
        {
            var hand = new List<Card>
            {
                new Card(Suit.Heart, 4),
                new Card(Suit.Diamond, 11),
                new Card(Suit.Spade, 4),  // 対象外
                new Card(Suit.Club, 11)   // 対象外
            };
            var reference = new Card(Suit.Heart, 1);

            var result = HandDiscardEvaluator.Evaluate(hand, RouletteCategory.Color, reference);

            Assert.AreEqual(2, result.Count);
            CollectionAssert.Contains(result, new Card(Suit.Heart, 4));
            CollectionAssert.Contains(result, new Card(Suit.Diamond, 11));
            CollectionAssert.DoesNotContain(result, new Card(Suit.Spade, 4));
            CollectionAssert.DoesNotContain(result, new Card(Suit.Club, 11));
        }

        [Test]
        public void Color_Black_MatchesSpadeAndClub()
        {
            var hand = new List<Card>
            {
                new Card(Suit.Spade, 6),
                new Card(Suit.Club, 12),
                new Card(Suit.Heart, 6),   // 対象外
                new Card(Suit.Diamond, 12) // 対象外
            };
            var reference = new Card(Suit.Club, 1);

            var result = HandDiscardEvaluator.Evaluate(hand, RouletteCategory.Color, reference);

            Assert.AreEqual(2, result.Count);
            CollectionAssert.Contains(result, new Card(Suit.Spade, 6));
            CollectionAssert.Contains(result, new Card(Suit.Club, 12));
            CollectionAssert.DoesNotContain(result, new Card(Suit.Heart, 6));
            CollectionAssert.DoesNotContain(result, new Card(Suit.Diamond, 12));
        }

        [Test]
        public void NoMatchingCards_ReturnsEmptyList_NoException()
        {
            var hand = new List<Card>
            {
                new Card(Suit.Spade, 2),
                new Card(Suit.Club, 3)
            };
            var reference = new Card(Suit.Heart, 9); // Rank/Suit/Colorいずれも手札と一致しない

            Assert.DoesNotThrow(() =>
            {
                var numberResult = HandDiscardEvaluator.Evaluate(hand, RouletteCategory.Number, reference);
                Assert.AreEqual(0, numberResult.Count);

                var suitResult = HandDiscardEvaluator.Evaluate(hand, RouletteCategory.Suit, reference);
                Assert.AreEqual(0, suitResult.Count);
            });
        }

        [Test]
        public void EmptyHand_ReturnsEmptyList()
        {
            var hand = new List<Card>();
            var reference = new Card(Suit.Heart, 9);

            var numberResult = HandDiscardEvaluator.Evaluate(hand, RouletteCategory.Number, reference);
            var suitResult = HandDiscardEvaluator.Evaluate(hand, RouletteCategory.Suit, reference);
            var colorResult = HandDiscardEvaluator.Evaluate(hand, RouletteCategory.Color, reference);

            Assert.AreEqual(0, numberResult.Count);
            Assert.AreEqual(0, suitResult.Count);
            Assert.AreEqual(0, colorResult.Count);
        }

        [Test]
        public void NullHand_Throws()
        {
            var reference = new Card(Suit.Heart, 9);
            Assert.Throws<ArgumentNullException>(
                () => HandDiscardEvaluator.Evaluate(null, RouletteCategory.Number, reference));
        }
    }
}
