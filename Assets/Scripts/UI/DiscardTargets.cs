using System;
using System.Collections.Generic;
using LuckyTrash.Cards;

namespace LuckyTrash.UI
{
    /// <summary>
    /// 判定前の手札の並びと、判定で捨てられたカードの一覧から、手札のどの位置のカードを捨て札の山へ飛ばすかを決める純粋関数。
    /// カードはスートとランクで一意（同じカードは1枚しかない）なので、値の一致で位置を特定する。
    /// </summary>
    public static class DiscardTargets
    {
        /// <summary>
        /// 捨てられたカードが、判定前の手札の何番目（0始まり）にあったかを、小さい順に返す。
        /// 捨てられたカードが手札に無い場合は無視する（同じ位置を2回返すことはない）。
        /// </summary>
        public static IReadOnlyList<int> GetIndices(IReadOnlyList<Card> handBefore, IReadOnlyList<Card> discarded)
        {
            if (handBefore == null)
            {
                throw new ArgumentNullException(nameof(handBefore));
            }
            if (discarded == null)
            {
                throw new ArgumentNullException(nameof(discarded));
            }

            var indices = new List<int>();
            var used = new bool[handBefore.Count];
            foreach (var card in discarded)
            {
                for (int i = 0; i < handBefore.Count; i++)
                {
                    if (!used[i] && handBefore[i].Equals(card))
                    {
                        used[i] = true;
                        indices.Add(i);
                        break;
                    }
                }
            }
            indices.Sort();
            return indices;
        }
    }
}
