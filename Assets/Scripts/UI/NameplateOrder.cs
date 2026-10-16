using System;
using System.Collections.Generic;
using System.Linq;

namespace LuckyTrash.UI
{
    /// <summary>
    /// 画面左上のネームプレートを並べる順番（1P, 2P, ...）を決める純粋関数。
    /// GameState の手番進行と同じく、SeatIndex の昇順（時計回り）に、開始プレイヤーの座席から
    /// 1周した順を返す。ゲームロジックは読み取りのみで、ここでは座席番号の並べ替えだけを行う。
    /// </summary>
    public static class NameplateOrder
    {
        /// <summary>
        /// 参加している座席番号の一覧と開始プレイヤーの座席番号から、1P から順に並べた座席番号の一覧を返す。
        /// </summary>
        /// <param name="seatIndices">参加しているプレイヤーの座席番号（順不同、重複なし）。</param>
        /// <param name="startingSeatIndex">その試合で最初に手番が来るプレイヤーの座席番号。</param>
        public static IReadOnlyList<int> GetTurnOrder(IEnumerable<int> seatIndices, int startingSeatIndex)
        {
            if (seatIndices == null)
            {
                throw new ArgumentNullException(nameof(seatIndices));
            }

            var sorted = seatIndices.OrderBy(i => i).ToList();
            int start = sorted.IndexOf(startingSeatIndex);
            if (start < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(startingSeatIndex), startingSeatIndex, "The starting seat is not among the seat indices.");
            }

            var order = new List<int>(sorted.Count);
            for (int offset = 0; offset < sorted.Count; offset++)
            {
                order.Add(sorted[(start + offset) % sorted.Count]);
            }
            return order;
        }
    }
}
