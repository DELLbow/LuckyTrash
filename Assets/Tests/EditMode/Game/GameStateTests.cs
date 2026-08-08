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

        // ------------------------------------------------------------------
        // SpinCategory / DrawReferenceCard / ResolveTurn（PlayTurn を分割した3メソッド）。
        // 「1枚引く」「ルーレットを回す」の2ボタン制UIから個別に呼び出せることを検証する。
        // ------------------------------------------------------------------

        [Test]
        public void SpinCategory_ReturnsValidCategory()
        {
            var setupResult = GameSetup.SetUp(2, new Random(1));
            var selector = new RouletteSelector(new Random(2));
            var state = new GameState(setupResult, selector, startingSeatIndex: 0);

            var category = state.SpinCategory();

            Assert.IsTrue(Enum.IsDefined(typeof(RouletteCategory), category));
        }

        [Test]
        public void SpinCategory_DelegatesToTheSameRouletteSelectorInstance_SameSequenceAsDirectUse()
        {
            // GameState.SpinCategory() は、コンストラクタで渡された RouletteSelector の
            // Spin() をそのまま呼んでいるだけであることを、同じシードの別インスタンスと比較して確認する。
            var setupResult = GameSetup.SetUp(2, new Random(1));
            const int sharedSeed = 42;
            var selectorForState = new RouletteSelector(new Random(sharedSeed));
            var state = new GameState(setupResult, selectorForState, startingSeatIndex: 0);

            var independentSelector = new RouletteSelector(new Random(sharedSeed));

            for (int i = 0; i < 10; i++)
            {
                var fromState = state.SpinCategory();
                var fromIndependent = independentSelector.Spin();
                Assert.AreEqual(fromIndependent, fromState);
            }
        }

        [Test]
        public void SpinCategory_AfterGameEnded_Throws()
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

            Assert.Throws<InvalidOperationException>(() => state.SpinCategory());
        }

        [Test]
        public void DrawReferenceCard_ReducesFlipDeckCount_AndAddsDrawnCardToDiscardPile()
        {
            var setupResult = GameSetup.SetUp(2, new Random(1));
            var selector = new RouletteSelector(new Random(2));
            var state = new GameState(setupResult, selector, startingSeatIndex: 0);

            int flipDeckCountBefore = state.FlipDeckCount;
            int discardPileCountBefore = state.DiscardPile.Count;

            var (card, reconstituted) = state.DrawReferenceCard();

            Assert.AreEqual(flipDeckCountBefore - 1, state.FlipDeckCount);
            Assert.AreEqual(discardPileCountBefore + 1, state.DiscardPile.Count);
            Assert.AreEqual(card, state.DiscardPile[state.DiscardPile.Count - 1]);
            Assert.IsFalse(reconstituted);
        }

        [Test]
        public void DrawReferenceCard_WhenFlipDeckEmpty_ReconstitutesFromDiscardPile()
        {
            var setupResult = GameSetup.SetUp(2, new Random(1));
            var selector = new RouletteSelector(new Random(2));
            var state = new GameState(setupResult, selector, startingSeatIndex: 0);

            // 山札(52枚)を使い切る。
            while (state.FlipDeckCount > 0)
            {
                state.DrawReferenceCard();
            }

            Assert.AreEqual(0, state.FlipDeckCount);
            Assert.AreEqual(52, state.DiscardPile.Count);

            var (card, reconstituted) = state.DrawReferenceCard();

            Assert.IsTrue(reconstituted);
            // 再構築(52枚合流+shuffle)→ 1枚引く: FlipDeckCount=51, DiscardPile には今引いた1枚だけが積まれる。
            Assert.AreEqual(51, state.FlipDeckCount);
            Assert.AreEqual(1, state.DiscardPile.Count);
            Assert.AreEqual(card, state.DiscardPile[0]);
        }

        [Test]
        public void DrawReferenceCard_AfterGameEnded_Throws()
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

            Assert.Throws<InvalidOperationException>(() => state.DrawReferenceCard());
        }

        [Test]
        public void ResolveTurn_MatchingReferenceCard_DiscardsFromHand_AndConfirmsRank()
        {
            var (state, p0, _, _) = CreateThreePlayerFixture();

            var referenceCard = new Card(Suit.Spade, 4); // p0 の手札と完全一致 → 必ず捨てられる
            var result = state.ResolveTurn(RouletteCategory.Number, referenceCard, flipDeckWasReconstituted: false);

            Assert.AreSame(p0, result.TurnPlayer);
            Assert.IsFalse(result.WasPass);
            CollectionAssert.AreEqual(new[] { referenceCard }, result.DiscardedCards);
            Assert.AreEqual(0, p0.Hand.Count);
            Assert.AreEqual(1, p0.Rank);
            Assert.AreSame(p0, result.FinishedPlayer);
        }

        [Test]
        public void ResolveTurn_NoMatchingReferenceCard_ResultsInPass_HandUnchanged()
        {
            var (state, p0, _, _) = CreateThreePlayerFixture();

            // p0の手札(♠4)と一致しない基準カードを使う。
            var referenceCard = new Card(Suit.Heart, 9);
            var result = state.ResolveTurn(RouletteCategory.Number, referenceCard, flipDeckWasReconstituted: false);

            Assert.IsTrue(result.WasPass);
            Assert.AreEqual(0, result.DiscardedCards.Count);
            Assert.IsNull(result.FinishedPlayer);
            Assert.AreEqual(1, p0.Hand.Count, "パスの場合、手札は変化しない。");
        }

        [Test]
        public void ResolveTurn_FlipDeckWasReconstituted_IsReflectedInTurnResult()
        {
            var (state, _, _, _) = CreateThreePlayerFixture();

            var result = state.ResolveTurn(RouletteCategory.Color, new Card(Suit.Heart, 1), flipDeckWasReconstituted: true);

            Assert.IsTrue(result.FlipDeckWasReconstituted);
        }

        [Test]
        public void ResolveTurn_AfterGameEnded_Throws()
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

            Assert.Throws<InvalidOperationException>(
                () => state.ResolveTurn(RouletteCategory.Number, new Card(Suit.Heart, 9), false));
        }

        [Test]
        public void SplitMethods_CalledInSequence_ProduceSameResultAsPlayTurn()
        {
            // 同じ盤面から、(A) PlayTurn() を1回呼ぶ場合と、
            // (B) SpinCategory→DrawReferenceCard→ResolveTurn を手動で順番に呼ぶ場合とで、
            // 全く同じ結果になることを確認する（分割によって挙動が変わっていないことの直接的な検証）。
            var setupResultA = GameSetup.SetUp(3, new Random(7));
            var setupResultB = GameSetup.SetUp(3, new Random(7));

            var stateA = new GameState(setupResultA, new RouletteSelector(new Random(13)), startingSeatIndex: 0);
            var stateB = new GameState(setupResultB, new RouletteSelector(new Random(13)), startingSeatIndex: 0);

            var resultA = stateA.PlayTurn();

            var category = stateB.SpinCategory();
            var (card, reconstituted) = stateB.DrawReferenceCard();
            var resultB = stateB.ResolveTurn(category, card, reconstituted);

            Assert.AreEqual(resultA.TurnPlayer.SeatIndex, resultB.TurnPlayer.SeatIndex);
            Assert.AreEqual(resultA.Category, resultB.Category);
            Assert.AreEqual(resultA.DrawnCard, resultB.DrawnCard);
            CollectionAssert.AreEqual(resultA.DiscardedCards, resultB.DiscardedCards);
            Assert.AreEqual(resultA.WasPass, resultB.WasPass);
            Assert.AreEqual(resultA.FlipDeckWasReconstituted, resultB.FlipDeckWasReconstituted);
            Assert.AreEqual(resultA.GameEnded, resultB.GameEnded);
            Assert.AreEqual(stateA.FlipDeckCount, stateB.FlipDeckCount);
        }
    }
}
