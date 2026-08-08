using System;
using System.Collections.Generic;

namespace LuckyTrash.Cards
{
    /// <summary>
    /// 52枚（Suit 4種 × Rank 1〜13）を扱うデッキ。
    /// 「手札配布用デッキ」「めくり札用デッキ」等、用途ごとに独立したインスタンスとして生成して使う。
    /// </summary>
    public class Deck
    {
        private static readonly Suit[] AllSuits =
        {
            Suit.Spade, Suit.Heart, Suit.Diamond, Suit.Club
        };

        private readonly List<Card> _cards = new List<Card>();
        private readonly Random _random;

        /// <summary>
        /// 現在デッキに残っているカードの枚数。
        /// </summary>
        public int Count => _cards.Count;

        /// <summary>
        /// デッキが空かどうか。
        /// </summary>
        public bool IsEmpty => _cards.Count == 0;

        /// <summary>
        /// 空のデッキを生成する。
        /// </summary>
        /// <param name="random">
        /// シャッフルに使用する乱数生成器。省略時は既定の System.Random を使用する。
        /// テストで結果を固定したい場合はシード付きの Random を渡す。
        /// </param>
        public Deck(Random random = null)
        {
            _random = random ?? new Random();
        }

        /// <summary>
        /// Suit 4種 × Rank 1〜13 の52枚が揃った、標準的なトランプデッキを生成する。
        /// </summary>
        public static Deck CreateStandard52(Random random = null)
        {
            var deck = new Deck(random);
            deck.Fill();
            return deck;
        }

        /// <summary>
        /// デッキを空にしたうえで、52枚（未シャッフル）を積み直す。
        /// </summary>
        public void Fill()
        {
            _cards.Clear();
            foreach (var suit in AllSuits)
            {
                for (int rank = Card.MinRank; rank <= Card.MaxRank; rank++)
                {
                    _cards.Add(new Card(suit, rank));
                }
            }
        }

        /// <summary>
        /// Fisher-Yates アルゴリズムでデッキ内のカードをシャッフルする。
        /// </summary>
        public void Shuffle()
        {
            for (int i = _cards.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (_cards[i], _cards[j]) = (_cards[j], _cards[i]);
            }
        }

        /// <summary>
        /// 先頭（末尾）から1枚引く。デッキが空の場合は例外を投げる。
        /// 例外を避けたい場合は <see cref="TryDraw"/> を使用する。
        /// </summary>
        public Card Draw()
        {
            if (!TryDraw(out var card))
            {
                throw new InvalidOperationException("Deck is empty; cannot draw a card.");
            }

            return card;
        }

        /// <summary>
        /// 1枚引くことを試みる。デッキが空の場合は false を返し、card には default(Card) が入る。
        /// </summary>
        public bool TryDraw(out Card card)
        {
            if (_cards.Count == 0)
            {
                card = default;
                return false;
            }

            int lastIndex = _cards.Count - 1;
            card = _cards[lastIndex];
            _cards.RemoveAt(lastIndex);
            return true;
        }

        /// <summary>
        /// 捨て札（またはその他の合流させたいカード群）をデッキに合流させ、再シャッフルする。
        /// 主に「めくり札用デッキ」が尽きたときに、捨て札を新しいデッキとして復元するために使う。
        /// 呼び出し時点でデッキに残っているカードがあればそれも合流対象に含まれる。
        /// </summary>
        public void Reconstitute(IEnumerable<Card> discardPile)
        {
            if (discardPile == null)
            {
                throw new ArgumentNullException(nameof(discardPile));
            }

            _cards.AddRange(discardPile);
            Shuffle();
        }

        /// <summary>
        /// 現在デッキに残っているカードを読み取り専用で参照する（デバッグ・検証用）。
        /// </summary>
        public IReadOnlyList<Card> Peek() => _cards.AsReadOnly();
    }
}
