using System;
using System.Collections.Generic;
using System.Linq;
using LuckyTrash.Cards;
using NUnit.Framework;

namespace LuckyTrash.Cards.Tests
{
    public class DeckTests
    {
        [Test]
        public void CreateStandard52_Has52UniqueCards()
        {
            var deck = Deck.CreateStandard52();

            Assert.AreEqual(52, deck.Count);

            var distinct = new HashSet<Card>(deck.Peek());
            Assert.AreEqual(52, distinct.Count);

            foreach (Suit suit in Enum.GetValues(typeof(Suit)))
            {
                int countForSuit = deck.Peek().Count(c => c.Suit == suit);
                Assert.AreEqual(13, countForSuit, $"Suit {suit} should have 13 cards.");
            }
        }

        [Test]
        public void Shuffle_DoesNotChangeCountOrContents()
        {
            var deck = Deck.CreateStandard52(new Random(12345));
            var before = new HashSet<Card>(deck.Peek());

            deck.Shuffle();

            Assert.AreEqual(52, deck.Count);
            var after = new HashSet<Card>(deck.Peek());
            Assert.IsTrue(before.SetEquals(after));
        }

        [Test]
        public void Shuffle_ChangesOrder_WithSeededRandom()
        {
            // 同じ内容でも順序は変わることを、シード固定の乱数で確認する
            // （理論上ごく低確率で同じ並びになり得るが、52枚のシャッフルでは現実的に無視できる）
            var deck = Deck.CreateStandard52(new Random(1));
            var before = deck.Peek().ToList();

            deck.Shuffle();
            var after = deck.Peek().ToList();

            CollectionAssert.AreNotEqual(before, after);
        }

        [Test]
        public void Draw_ReducesCountByOne_AndReturnsRemovedCard()
        {
            var deck = Deck.CreateStandard52();
            var expected = deck.Peek()[deck.Count - 1];

            var drawn = deck.Draw();

            Assert.AreEqual(expected, drawn);
            Assert.AreEqual(51, deck.Count);
            CollectionAssert.DoesNotContain(deck.Peek().ToList(), drawn);
        }

        [Test]
        public void Draw_AllCards_ThenDrawAgain_Throws()
        {
            var deck = Deck.CreateStandard52();

            for (int i = 0; i < 52; i++)
            {
                deck.Draw();
            }

            Assert.IsTrue(deck.IsEmpty);
            Assert.Throws<InvalidOperationException>(() => deck.Draw());
        }

        [Test]
        public void TryDraw_OnEmptyDeck_ReturnsFalse_AndDoesNotThrow()
        {
            var deck = new Deck(); // 空のデッキ

            bool result = deck.TryDraw(out var card);

            Assert.IsFalse(result);
            Assert.AreEqual(default(Card), card);
        }

        [Test]
        public void TwoDecks_AreIndependent_HandDeckAndFlipDeckDoNotMix()
        {
            var handDeck = Deck.CreateStandard52();
            var flipDeck = Deck.CreateStandard52();

            handDeck.Draw();
            handDeck.Draw();

            Assert.AreEqual(50, handDeck.Count);
            Assert.AreEqual(52, flipDeck.Count, "flipDeck should be unaffected by handDeck draws.");
        }

        [Test]
        public void Reconstitute_MergesDiscardPile_AndReshufflesIntoNewDeck()
        {
            var flipDeck = Deck.CreateStandard52(new Random(42));
            var discardPile = new List<Card>();

            // めくり札用デッキを使い切り、引いたカードを捨て札にする想定
            while (!flipDeck.IsEmpty)
            {
                discardPile.Add(flipDeck.Draw());
            }

            Assert.AreEqual(0, flipDeck.Count);
            Assert.AreEqual(52, discardPile.Count);

            flipDeck.Reconstitute(discardPile);

            Assert.AreEqual(52, flipDeck.Count);
            var reconstituted = new HashSet<Card>(flipDeck.Peek());
            var original = new HashSet<Card>(discardPile);
            Assert.IsTrue(original.SetEquals(reconstituted));
        }

        [Test]
        public void Reconstitute_NullDiscardPile_Throws()
        {
            var deck = new Deck();
            Assert.Throws<ArgumentNullException>(() => deck.Reconstitute(null));
        }
    }
}
