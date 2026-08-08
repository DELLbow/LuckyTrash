using System;
using System.Collections.Generic;
using LuckyTrash.Cards;

namespace LuckyTrash.Game
{
    /// <summary>
    /// 4人打ちテーブルにおける座席位置。SeatIndex（0始まり）と対応する。
    /// 0: 下辺 / 1: 右辺 / 2: 上辺 / 3: 左辺（基本設計書3.2節）。
    /// </summary>
    public enum SeatPosition
    {
        Bottom = 0,
        Right = 1,
        Top = 2,
        Left = 3
    }

    /// <summary>
    /// プレイヤー1人分の状態（座席・手札・順位）を表す。
    /// </summary>
    public class Player
    {
        private readonly List<Card> _hand = new List<Card>();

        /// <summary>
        /// 座席番号（0始まり）。4人打ちの場合、下辺→右辺→上辺→左辺の順に対応する。
        /// </summary>
        public int SeatIndex { get; }

        /// <summary>
        /// 座席番号に対応する座席位置（0〜3: Bottom/Right/Top/Left）。
        /// </summary>
        public SeatPosition SeatPosition => (SeatPosition)SeatIndex;

        /// <summary>
        /// 現在の手札。外部からは読み取り専用として見える。
        /// </summary>
        public IReadOnlyList<Card> Hand => _hand;

        /// <summary>
        /// あがった時点で確定する順位。未確定の間は null。
        /// </summary>
        public int? Rank { get; private set; }

        /// <summary>
        /// 手札を0枚にしてあがった状態かどうか。
        /// </summary>
        public bool IsEliminated => _hand.Count == 0;

        public Player(int seatIndex)
        {
            if (seatIndex < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(seatIndex), seatIndex, "SeatIndex must be zero or greater.");
            }

            SeatIndex = seatIndex;
        }

        /// <summary>
        /// 手札に1枚加える。
        /// </summary>
        public void AddToHand(Card card)
        {
            _hand.Add(card);
        }

        /// <summary>
        /// 手札に複数枚まとめて加える。
        /// </summary>
        public void AddToHand(IEnumerable<Card> cards)
        {
            if (cards == null)
            {
                throw new ArgumentNullException(nameof(cards));
            }

            _hand.AddRange(cards);
        }

        /// <summary>
        /// 手札から指定したカード群を取り除く。
        /// HandDiscardEvaluator の判定結果（捨てるべきカードの一覧）をそのまま渡し、
        /// 複数枚まとめて捨てることを想定している。
        /// 手札に存在しないカードが含まれていても、その分は無視して残りを取り除く。
        /// </summary>
        public void RemoveFromHand(IEnumerable<Card> cardsToRemove)
        {
            if (cardsToRemove == null)
            {
                throw new ArgumentNullException(nameof(cardsToRemove));
            }

            foreach (var card in cardsToRemove)
            {
                _hand.Remove(card);
            }
        }

        /// <summary>
        /// あがった際の順位を確定する。一度確定した順位は上書きされる。
        /// </summary>
        public void ConfirmRank(int rank)
        {
            if (rank < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(rank), rank, "Rank must be 1 or greater.");
            }

            Rank = rank;
        }
    }
}
