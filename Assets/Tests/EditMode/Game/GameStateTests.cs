using System;
using System.Collections.Generic;
using System.Linq;
using LuckyTrash.Cards;
using NUnit.Framework;

namespace LuckyTrash.Game.Tests
{
    public class GameStateTests
    {
        // ------------------------------------------------------------------
        // 実際の GameSetup + RouletteSelector を組み合わせた、構成そのものの疎通確認。
        // ------------------------------------------------------------------

        [Test]
        public void PlayTurn_WithRealSetup_ReturnsStructurallyValidTurnResult()
        {
            var setupResult = GameSetup.SetUp(4, new Random(1));
            var selector = new RouletteSelector(new Random(2));
            var state = new GameState(setupResult, selector, startingSeatIndex: 0);

            var expectedTurnPlayer = state.CurrentPlayer;
            int handCountBefore = expectedTurnPlayer.Hand.Count;

            var result = state.PlayTurn();

            Assert.AreSame(expectedTurnPlayer, result.TurnPlayer);
            Assert.IsTrue(Enum.IsDefined(typeof(RouletteCategory), result.Category));
            Assert.IsTrue(result.DrawnCard.Rank >= Card.MinRank && result.DrawnCard.Rank <= Card.MaxRank);
            Assert.AreEqual(1 + result.DiscardedCards.Count, state.DiscardPile.Count);

            if (result.WasPass)
            {
                Assert.AreEqual(0, result.DiscardedCards.Count);
                Assert.AreEqual(handCountBefore, expectedTurnPlayer.Hand.Count);
                Assert.IsNull(result.FinishedPlayer);
            }
            else
            {
                Assert.AreEqual(handCountBefore - result.DiscardedCards.Count, expectedTurnPlayer.Hand.Count);
            }
        }

        // ------------------------------------------------------------------
        // 手札・めくり札を手動で作り込んだ、決定論的な3人プレイのシナリオ。
        // 基準カードと完全一致する1枚だけを持つ p0 が確実にあがり、
        // p1 / p2 はどのカテゴリが選ばれても一致しない（=必ずパスする）手札を持つ。
        // ------------------------------------------------------------------

        private static (GameState state, Player p0, Player p1, Player p2) CreateThreePlayerFixture()
        {
            var p0 = new Player(0);
            var p1 = new Player(1);
            var p2 = new Player(2);

            // 基準カードと完全一致 → Number/Suit/Color いずれのカテゴリでも必ず一致する。
            p0.AddToHand(new Card(Suit.Spade, 4));

            // Rank(4)・Suit(Spade)・Color(Black)のいずれとも一致しない手札 → 必ずパスする。
            p1.AddToHand(new[] { new Card(Suit.Heart, 7), new Card(Suit.Diamond, 9) });
            p2.AddToHand(new[] { new Card(Suit.Heart, 2), new Card(Suit.Diamond, 11) });

            var players = new List<Player> { p0, p1, p2 };

            // めくり札用デッキには、基準カードとなる Spade-4 を1枚だけ入れておく。
            var flipDeck = new Deck(new Random(1));
            flipDeck.Reconstitute(new[] { new Card(Suit.Spade, 4) });

            var setupResult = new GameSetupResult(players, flipDeck);
            var selector = new RouletteSelector(new Random(2));
            var state = new GameState(setupResult, selector, startingSeatIndex: 0);

            return (state, p0, p1, p2);
        }

        [Test]
        public void PlayTurn_MatchingCard_IsDiscarded_AndPlayerFinishes()
        {
            var (state, p0, _, _) = CreateThreePlayerFixture();

            var result = state.PlayTurn();

            Assert.AreEqual(new Card(Suit.Spade, 4), result.DrawnCard);
            Assert.IsFalse(result.WasPass);
            CollectionAssert.AreEqual(new[] { new Card(Suit.Spade, 4) }, result.DiscardedCards);

            Assert.AreEqual(0, p0.Hand.Count);
            Assert.IsTrue(p0.IsEliminated);
            Assert.AreEqual(1, p0.Rank);
            Assert.AreSame(p0, result.FinishedPlayer);

            // 3人プレイなのでこの時点ではまだゲームは終了しない。
            Assert.IsFalse(result.GameEnded);
            Assert.IsFalse(state.IsGameOver);

            // 捨て札置き場には「引いたカード」と「手札から捨てたカード」の2枚が積まれる。
            Assert.AreEqual(2, state.DiscardPile.Count);
        }

        [Test]
        public void PlayTurn_NoMatchingCards_ResultsInPass_HandUnchanged()
        {
            var (state, _, p1, _) = CreateThreePlayerFixture();

            state.PlayTurn(); // turn1: p0 があがる
            var result = state.PlayTurn(); // turn2: p1 の手番

            Assert.AreSame(p1, result.TurnPlayer);
            Assert.IsTrue(result.WasPass);
            Assert.AreEqual(0, result.DiscardedCards.Count);
            Assert.IsNull(result.FinishedPlayer);
            Assert.AreEqual(2, p1.Hand.Count, "パスの場合、手札は変化しない。");
        }

        [Test]
        public void PlayTurn_FinishedPlayer_IsSkippedInFutureTurns()
        {
            var (state, p0, p1, p2) = CreateThreePlayerFixture();

            var turn1 = state.PlayTurn(); // p0 があがる
            var turn2 = state.PlayTurn(); // p1 の手番（p0はスキップされる）
            var turn3 = state.PlayTurn(); // p2 の手番

            Assert.AreSame(p0, turn1.TurnPlayer);
            Assert.AreSame(p1, turn2.TurnPlayer);
            Assert.AreSame(p2, turn3.TurnPlayer);

            // p0 は既にあがっているので、以後どの手番プレイヤーにもならない。
            Assert.AreNotSame(p0, turn2.TurnPlayer);
            Assert.AreNotSame(p0, turn3.TurnPlayer);

            // p2 の次は p0 を飛ばして p1 に戻る。
            Assert.AreSame(p1, state.CurrentPlayer);
        }

        [Test]
        public void PlayTurn_FlipDeckReconstitution_WorksAndAllowsContinuedPlay()
        {
            var (state, _, _, _) = CreateThreePlayerFixture();

            // turn1: 初期状態でデッキに1枚だけあるので、まだ再構築は起きない。
            var turn1 = state.PlayTurn();
            Assert.IsFalse(turn1.FlipDeckWasReconstituted);

            // turn2: デッキが尽きているので、捨て札置き場から再構築されるはず。
            var turn2 = state.PlayTurn();
            Assert.IsTrue(turn2.FlipDeckWasReconstituted);
            // 再構築後に引かれるカードも、捨て札置き場に積まれていた Spade-4 と同一の値になる。
            Assert.AreEqual(new Card(Suit.Spade, 4), turn2.DrawnCard);

            // turn3: 例外なく継続してプレイできること（再構築後もゲームが進行し続ける）。
            Assert.DoesNotThrow(() => state.PlayTurn());
        }

        // ------------------------------------------------------------------
        // ゲーム終了関連
        // ------------------------------------------------------------------

        [Test]
        public void PlayTurn_TwoPlayers_LastActivePlayer_GetsAutoAssignedLastPlace_AndGameEnds()
        {
            var p0 = new Player(0);
            var p1 = new Player(1);
            p0.AddToHand(new Card(Suit.Spade, 4));
            p1.AddToHand(new Card(Suit.Heart, 9)); // 内容は問わない

            var players = new List<Player> { p0, p1 };
            var flipDeck = new Deck(new Random(1));
            flipDeck.Reconstitute(new[] { new Card(Suit.Spade, 4) });

            var setupResult = new GameSetupResult(players, flipDeck);
            var selector = new RouletteSelector(new Random(2));
            var state = new GameState(setupResult, selector, startingSeatIndex: 0);

            var result = state.PlayTurn();

            Assert.IsTrue(result.GameEnded);
            Assert.IsTrue(state.IsGameOver);
            Assert.IsNull(state.CurrentPlayer);

            Assert.AreEqual(1, p0.Rank);
            Assert.AreEqual(2, p1.Rank);

            var rankings = state.Rankings;
            Assert.AreEqual(2, rankings.Count);
            Assert.AreSame(p0, rankings[0]);
            Assert.AreSame(p1, rankings[1]);
        }

        [Test]
        public void PlayTurn_AfterGameEnded_Throws()
        {
            var p0 = new Player(0);
            var p1 = new Player(1);
            p0.AddToHand(new Card(Suit.Spade, 4));
            p1.AddToHand(new Card(Suit.Heart, 9));

            var players = new List<Player> { p0, p1 };
            var flipDeck = new Deck(new Random(1));
            flipDeck.Reconstitute(new[] { new Card(Suit.Spade, 4) });

            var setupResult = new GameSetupResult(players, flipDeck);
            var selector = new RouletteSelector(new Random(2));
            var state = new GameState(setupResult, selector, startingSeatIndex: 0);

            state.PlayTurn(); // ここでゲーム終了

            Assert.IsTrue(state.IsGameOver);
            Assert.Throws<InvalidOperationException>(() => state.PlayTurn());
        }

        // ------------------------------------------------------------------
        // 現実的な乱数構成での2人プレイ、決着までの通しシナリオ。
        // ------------------------------------------------------------------

        [Test]
        public void PlayTurn_TwoPlayerFullGame_EventuallyEndsWithDistinctRanks()
        {
            var setupResult = GameSetup.SetUp(2, new Random(42));
            var selector = new RouletteSelector(new Random(99));
            var state = new GameState(setupResult, selector, startingSeatIndex: 0);

            const int maxTurns = 5000;
            int turnsPlayed = 0;

            while (!state.IsGameOver && turnsPlayed < maxTurns)
            {
                state.PlayTurn();
                turnsPlayed++;
            }

            Assert.IsTrue(state.IsGameOver, $"{maxTurns}ターン以内にゲームが終了しなかった。");
            Assert.IsNull(state.CurrentPlayer);

            var rankings = state.Rankings;
            Assert.AreEqual(2, rankings.Count);

            var ranks = rankings.Select(p => p.Rank.Value).ToList();
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, ranks, "順位は1位・2位が重複なく設定されること。");

            // ゲーム終了後に PlayTurn を呼ぶと定義通り例外になること。
            Assert.Throws<InvalidOperationException>(() => state.PlayTurn());
        }
    }
}
