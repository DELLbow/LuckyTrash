using System;

namespace LuckyTrash.Cards
{
    /// <summary>
    /// トランプのマーク（スート）。
    /// </summary>
    public enum Suit
    {
        Spade,
        Heart,
        Diamond,
        Club
    }

    /// <summary>
    /// カードの色。Suit から自動的に導出される。
    /// </summary>
    public enum CardColor
    {
        Black,
        Red
    }

    /// <summary>
    /// 1枚のトランプを表す不変（immutable）なデータ構造。
    /// 生成後に Suit / Rank が変化することはない。
    /// </summary>
    [Serializable]
    public readonly struct Card : IEquatable<Card>
    {
        public const int MinRank = 1;
        public const int MaxRank = 13;

        public Suit Suit { get; }
        public int Rank { get; }

        public Card(Suit suit, int rank)
        {
            if (rank < MinRank || rank > MaxRank)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(rank), rank, $"Rank must be between {MinRank} and {MaxRank}.");
            }

            Suit = suit;
            Rank = rank;
        }

        /// <summary>
        /// Heart / Diamond は赤、Spade / Club は黒。
        /// </summary>
        public CardColor Color => Suit == Suit.Heart || Suit == Suit.Diamond
            ? CardColor.Red
            : CardColor.Black;

        public bool Equals(Card other) => Suit == other.Suit && Rank == other.Rank;

        public override bool Equals(object obj) => obj is Card other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Suit, Rank);

        public override string ToString() => $"{Suit} {Rank}";

        public static bool operator ==(Card left, Card right) => left.Equals(right);

        public static bool operator !=(Card left, Card right) => !left.Equals(right);
    }
}
