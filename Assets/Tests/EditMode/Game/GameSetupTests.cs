using System;
using System.Collections.Generic;
using System.Linq;
using LuckyTrash.Cards;
using NUnit.Framework;

namespace LuckyTrash.Game.Tests
{
    public class GameSetupTests
    {
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void SetUp_ValidPlayerCount_CreatesCorrectNumberOfPlayers_InSeatOrder(int playerCount)
        {
            var result = GameSetup.SetUp(playerCount, new Random(1));

            Assert.AreEqual(playerCount, result.Players.Count);

            for (int i = 0; i < playerCount; i++)
            {
                Assert.AreEqual(i, result.Players[i].SeatIndex,
                    "Player は座席順（下辺→右辺→上辺→左辺、SeatIndex 0始まり）に並んでいること。");
            }
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void SetUp_EachPlayer_HasExactlySixCards(int playerCount)
        {
            var result = GameSetup.SetUp(playerCount, new Random(1));

            foreach (var player in result.Players)
            {
                Assert.AreEqual(GameSetup.InitialHandSize, player.Hand.Count);
            }
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void SetUp_AllPlayerHands_HaveNoDuplicateCardsAcrossPlayers(int playerCount)
        {
            var result = GameSetup.SetUp(playerCount, new Random(1));

            var allDealtCards = result.Players.SelectMany(p => p.Hand).ToList();

            Assert.AreEqual(playerCount * GameSetup.InitialHandSize, allDealtCards.Count);

            var distinct = new HashSet<Card>(allDealtCards);
            Assert.AreEqual(allDealtCards.Count, distinct.Count,
                "同じカードが2人以上のプレイヤーに配られていないこと。");
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void SetUp_FlipDeck_Has52Cards_AndIsIndependentFromHandDeck(int playerCount)
        {
            var result = GameSetup.SetUp(playerCount, new Random(1));

            // 手札配布用デッキからは playerCount * 6 枚が引かれている。
            // めくり札用デッキが手札配布用デッキと同一インスタンスであれば、
            // その分だけ枚数が減っているはずだが、52枚のままであることから
            // 独立した別インスタンスであることが確認できる。
            Assert.AreEqual(52, result.FlipDeck.Count);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(5)]
        [TestCase(-1)]
        public void SetUp_OutOfRangePlayerCount_Throws(int playerCount)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => GameSetup.SetUp(playerCount, new Random(1)));
        }

        [Test]
        public void SetUp_WithoutExplicitRandom_StillProducesValidSetup()
        {
            var result = GameSetup.SetUp(4);

            Assert.AreEqual(4, result.Players.Count);
            Assert.AreEqual(52, result.FlipDeck.Count);
            foreach (var player in result.Players)
            {
                Assert.AreEqual(GameSetup.InitialHandSize, player.Hand.Count);
            }
        }
    }
}
