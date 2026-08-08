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

        /// <summary>めくり札用デッキに現在残っているカードの枚数（UI表示用）。</summary>
        public int FlipDeckCount => _flipDeck.Count;

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
        /// 1ターン分の処理を、抽選・ドロー・判定を一括で行う。
        /// 内部的には <see cref="SpinCategory"/> → <see cref="DrawReferenceCard"/> →
        /// <see cref="ResolveTurn"/> の順に呼び出しているだけで、ロジック自体はこれらの
        /// 分割前と完全に同一（乱数消費順も変わらない）。
        /// UI側で「1枚引く」「ルーレットを回す」を別々のタイミング・演出で行いたい場合は、
        /// この3メソッドを個別に呼び出せる。
        /// ゲームが既に終了している場合は <see cref="InvalidOperationException"/> を投げる。
        /// </summary>
        public TurnResult PlayTurn()
        {
            if (IsGameOver)
            {
                throw new InvalidOperationException("Cannot play a turn; the game has already ended.");
            }

            var category = SpinCategory();
            var (drawnCard, reconstituted) = DrawReferenceCard();
            return ResolveTurn(category, drawnCard, reconstituted);
        }

        /// <summary>
        /// RouletteSelectorでカテゴリを抽選する（PlayTurnの手順2に相当）。
        /// ルーレットの回転演出は、結果としてどの扇形に止まるかを事前に知る必要があるため、
        /// この抽選は演出の再生前に行う想定。
        /// </summary>
        public RouletteCategory SpinCategory()
        {
            if (IsGameOver)
            {
                throw new InvalidOperationException("Cannot spin the roulette; the game has already ended.");
            }

            return _rouletteSelector.Spin();
        }

        /// <summary>
        /// めくり札用Deckから1枚引く（PlayTurnの手順3〜4に相当）。
        /// 空の場合は捨て札置き場から再構築してから引く。引いたカードは捨て札置き場に加える。
        /// ドロー演出（山札→基準カードスロット）は、カードの中身が分からなくても再生できるため、
        /// この処理は演出の再生後に呼び出す想定（演出完了後に実際の中身を確定させる）。
        /// </summary>
        /// <returns>引いたカードと、めくり札用デッキの再構築が発生したかどうか。</returns>
        public (Card Card, bool Reconstituted) DrawReferenceCard()
        {
            if (IsGameOver)
            {
                throw new InvalidOperationException("Cannot draw a reference card; the game has already ended.");
            }

            bool reconstituted = false;
            if (_flipDeck.IsEmpty)
            {
                _flipDeck.Reconstitute(_discardPile);
                _discardPile.Clear();
                reconstituted = true;
            }

            var drawnCard = _flipDeck.Draw();
            _discardPile.Add(drawnCard);

            return (drawnCard, reconstituted);
        }

        /// <summary>
        /// 既に確定しているカテゴリ・基準カードを使って、現在の手番プレイヤーの手札を判定し、
        /// 捨て札処理・順位確定・ゲーム終了判定・手番送りまで行う（PlayTurnの手順5〜9に相当）。
        /// 「1枚引く」「ルーレットを回す」の両方の演出が完了した後に、UI側から呼び出す想定。
        /// </summary>
        /// <param name="category"><see cref="SpinCategory"/> で得られたカテゴリ。</param>
        /// <param name="drawnCard"><see cref="DrawReferenceCard"/> で得られた基準カード。</param>
        /// <param name="flipDeckWasReconstituted">
        /// <see cref="DrawReferenceCard"/> の戻り値の Reconstituted をそのまま渡す。
        /// </param>
        public TurnResult ResolveTurn(RouletteCategory category, Card drawnCard, bool flipDeckWasReconstituted)
        {
            if (IsGameOver)
            {
                throw new InvalidOperationException("Cannot resolve a turn; the game has already ended.");
            }

            // 1. 現在の手番プレイヤーを取得する。
            var turnPlayer = CurrentPlayer;

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
                finishedPlayer, flipDeckWasReconstituted, IsGameOver);
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
