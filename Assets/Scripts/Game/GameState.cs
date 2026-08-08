using System;
using System.Collections.Generic;
using System.Linq;
using LuckyTrash.Cards;

namespace LuckyTrash.Game
{
    /// <summary>
    /// PlayTurn 1回分の結果。呼び出し側（将来のUI）が表示に使う情報をまとめたもの。
    /// </summary>
    public sealed class TurnResult
    {
        /// <summary>この手番のプレイヤー。</summary>
        public Player TurnPlayer { get; }

        /// <summary>ルーレットで抽選された判定カテゴリ。</summary>
        public RouletteCategory Category { get; }

        /// <summary>めくり札用デッキから引かれたカード（今回の基準カード）。</summary>
        public Card DrawnCard { get; }

        /// <summary>手番プレイヤーの手札から取り除かれ、捨て札置き場に加えられたカード（該当なしの場合は空）。</summary>
        public IReadOnlyList<Card> DiscardedCards { get; }

        /// <summary>該当カードが1枚もなく、何も捨てられなかった（パスした）かどうか。</summary>
        public bool WasPass { get; }

        /// <summary>この手番で手札が0枚になり、順位が確定したプレイヤー（いなければ null）。</summary>
        public Player FinishedPlayer { get; }

        /// <summary>この手番でめくり札用デッキが尽き、捨て札置き場から再構築されたかどうか。</summary>
        public bool FlipDeckWasReconstituted { get; }

        /// <summary>この手番でゲームが終了したかどうか。</summary>
        public bool GameEnded { get; }

        public TurnResult(
            Player turnPlayer,
            RouletteCategory category,
            Card drawnCard,
            IReadOnlyList<Card> discardedCards,
            bool wasPass,
            Player finishedPlayer,
            bool flipDeckWasReconstituted,
            bool gameEnded)
        {
            TurnPlayer = turnPlayer ?? throw new ArgumentNullException(nameof(turnPlayer));
            Category = category;
            DrawnCard = drawnCard;
            DiscardedCards = discardedCards ?? Array.Empty<Card>();
            WasPass = wasPass;
            FinishedPlayer = finishedPlayer;
            FlipDeckWasReconstituted = flipDeckWasReconstituted;
            GameEnded = gameEnded;
        }
    }

    /// <summary>
    /// ターン進行を管理するクラス。
    /// Player / GameSetup(の結果) / RouletteSelector / HandDiscardEvaluator / Deck を組み合わせて使用する。
    /// </summary>
    public class GameState
    {
        private readonly Deck _flipDeck;
        private readonly RouletteSelector _rouletteSelector;
        private readonly List<Card> _discardPile = new List<Card>();
        private readonly List<Player> _seatOrder;

        /// <summary>全プレイヤー（座席順）。</summary>
        public IReadOnlyList<Player> Players { get; }

        /// <summary>現在の手番プレイヤー。ゲームが終了している場合は null。</summary>
        public Player CurrentPlayer { get; private set; }

        /// <summary>ゲームが終了状態かどうか。</summary>
        public bool IsGameOver { get; private set; }

        /// <summary>
        /// 捨て札置き場（めくり札として引かれ場に出たカード＋各プレイヤーが手札から捨てたカードの蓄積）。
        /// </summary>
        public IReadOnlyList<Card> DiscardPile => _discardPile;

        /// <summary>順位が確定したプレイヤーの一覧（順位の昇順）。</summary>
        public IReadOnlyList<Player> Rankings =>
            Players.Where(p => p.Rank.HasValue).OrderBy(p => p.Rank.Value).ToList();

        /// <summary>
        /// GameSetup の結果と RouletteSelector を受け取り、ゲーム状態を初期化する。
        /// </summary>
        /// <param name="setupResult">GameSetup.SetUp の結果（Playersとめくり札用Deck）。</param>
        /// <param name="rouletteSelector">カテゴリ抽選に使うルーレット。</param>
        /// <param name="startingSeatIndex">
        /// 開始プレイヤーの座席番号。じゃんけん等の勝敗はUI側で決定し、その結果をここに渡す想定。
        /// </param>
        public GameState(GameSetupResult setupResult, RouletteSelector rouletteSelector, int startingSeatIndex)
        {
            if (setupResult == null)
            {
                throw new ArgumentNullException(nameof(setupResult));
            }

            Players = setupResult.Players;
            _flipDeck = setupResult.FlipDeck;
            _rouletteSelector = rouletteSelector ?? throw new ArgumentNullException(nameof(rouletteSelector));

            // 座席番号順に並べておき、時計回りの手番進行の基準にする。
            _seatOrder = Players.OrderBy(p => p.SeatIndex).ToList();

            var startingPlayer = _seatOrder.FirstOrDefault(p => p.SeatIndex == startingSeatIndex);
            if (startingPlayer == null)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(startingSeatIndex), startingSeatIndex,
                    "No player exists with the specified starting seat index.");
            }

            CurrentPlayer = startingPlayer;
            IsGameOver = false;
        }

        /// <summary>
        /// 1ターン分の処理を行う。
        /// ゲームが既に終了している場合は <see cref="InvalidOperationException"/> を投げる。
        /// </summary>
        public TurnResult PlayTurn()
        {
            if (IsGameOver)
            {
                throw new InvalidOperationException("Cannot play a turn; the game has already ended.");
            }

            // 1. 現在の手番プレイヤーを取得する。
            var turnPlayer = CurrentPlayer;

            // 2. RouletteSelectorでカテゴリを抽選する。
            var category = _rouletteSelector.Spin();

            // 3. めくり札用Deckから1枚引く。空の場合は捨て札置き場から再構築してから引く。
            bool reconstituted = false;
            if (_flipDeck.IsEmpty)
            {
                _flipDeck.Reconstitute(_discardPile);
                _discardPile.Clear();
                reconstituted = true;
            }

            var drawnCard = _flipDeck.Draw();

            // 4. 引いたカードは手札には加えず、捨て札置き場に加える。
            _discardPile.Add(drawnCard);

            // 5. 手番プレイヤーの手札から条件を満たすカードを判定する。
            var discardTargets = HandDiscardEvaluator.Evaluate(turnPlayer.Hand, category, drawnCard);
            bool wasPass = discardTargets.Count == 0;

            // 6. 該当カードがあれば手札から取り除き、捨て札置き場に加える。無ければ何もしない（パス）。
            if (!wasPass)
            {
                turnPlayer.RemoveFromHand(discardTargets);
                foreach (var card in discardTargets)
                {
                    _discardPile.Add(card);
                }
            }

            // 7. 手札が0枚になったプレイヤーは、順位を確定し、以後の手番から除外する。
            Player finishedPlayer = null;
            if (turnPlayer.IsEliminated)
            {
                turnPlayer.ConfirmRank(NextRankToAssign());
                finishedPlayer = turnPlayer;
            }

            // 8. アクティブなプレイヤーが1人だけになったら、自動的に最下位の順位を設定してゲームを終了する。
            var activePlayers = Players.Where(p => !p.Rank.HasValue).ToList();
            if (activePlayers.Count <= 1)
            {
                if (activePlayers.Count == 1)
                {
                    activePlayers[0].ConfirmRank(NextRankToAssign());
                }

                IsGameOver = true;
                CurrentPlayer = null;
            }
            else
            {
                // 9. ゲームが終了していなければ、次のアクティブなプレイヤーに手番を進める。
                CurrentPlayer = GetNextActivePlayer(turnPlayer);
            }

            return new TurnResult(
                turnPlayer, category, drawnCard, discardTargets, wasPass,
                finishedPlayer, reconstituted, IsGameOver);
        }

        private int NextRankToAssign()
        {
            return Players.Count(p => p.Rank.HasValue) + 1;
        }

        private Player GetNextActivePlayer(Player fromPlayer)
        {
            int startIndex = _seatOrder.IndexOf(fromPlayer);
            int seatCount = _seatOrder.Count;

            for (int offset = 1; offset <= seatCount; offset++)
            {
                var candidate = _seatOrder[(startIndex + offset) % seatCount];
                if (!candidate.Rank.HasValue)
                {
                    return candidate;
                }
            }

            // アクティブなプレイヤーが1人だけの場合は呼び出し元(8.)でゲーム終了として扱われるため、
            // ここに到達することはない想定。
            throw new InvalidOperationException("No active players remain.");
        }
    }
}
