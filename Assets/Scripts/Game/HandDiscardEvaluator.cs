using System;
using System.Collections.Generic;
using LuckyTrash.Cards;

namespace LuckyTrash.Game
{
    /// <summary>
    /// ルーレットで決まった判定カテゴリと、めくり札用デッキから引いた基準カードをもとに、
    /// 手札の中で条件を満たす（=捨てるべき）カードを判定する。
    /// </summary>
    public static class HandDiscardEvaluator
    {
        /// <summary>
        /// 手札の中から、判定カテゴリと基準カードの条件に一致するカードを全て抽出する。
        /// 該当するカードが1枚もない場合は空のリストを返す（例外は投げない。「パス」に対応）。
        /// </summary>
        /// <param name="hand">判定対象の手札。</param>
        /// <param name="category">
        /// 判定カテゴリ。
        /// Number: 基準カードと同じRank（マーク・色は問わない）。
        /// Suit: 基準カードと同じSuit（数字・色は問わない）。
        /// Color: 基準カードと同じColor（数字・マークは問わない）。
        /// </param>
        /// <param name="referenceCard">めくり札用デッキから引いた基準カード。</param>
        /// <returns>条件を満たす（捨てるべき）手札の一覧。該当なしの場合は空のリスト。</returns>
        public static IReadOnlyList<Card> Evaluate(
            IReadOnlyList<Card> hand, RouletteCategory category, Card referenceCard)
        {
            if (hand == null)
            {
                throw new ArgumentNullException(nameof(hand));
            }

            var targets = new List<Card>();

            foreach (var card in hand)
            {
                if (Matches(card, category, referenceCard))
                {
                    targets.Add(card);
                }
            }

            return targets;
        }

        private static bool Matches(Card card, RouletteCategory category, Card referenceCard)
        {
            switch (category)
            {
                case RouletteCategory.Number:
                    return card.Rank == referenceCard.Rank;
                case RouletteCategory.Suit:
                    return card.Suit == referenceCard.Suit;
                case RouletteCategory.Color:
                    return card.Color == referenceCard.Color;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(category), category, "Unsupported RouletteCategory.");
            }
        }
    }
}
