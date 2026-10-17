using System;
using UnityEngine;

namespace LuckyTrash.UI.Scoring
{
    /// <summary>
    /// 得点のルール（順位の得点と、運の良い出来事のボーナス）。値は Inspector（<see cref="ScoreRulesAsset"/>）で調整できる。
    /// </summary>
    [Serializable]
    public class ScoreRules
    {
        [Header("順位の得点（1位から順）")]
        [Tooltip("2人対戦の順位の得点。")]
        public int[] RankPoints2 = { 100, 0 };
        [Tooltip("3人対戦の順位の得点。")]
        public int[] RankPoints3 = { 100, 50, 0 };
        [Tooltip("4人対戦の順位の得点。")]
        public int[] RankPoints4 = { 100, 60, 30, 0 };

        [Header("ビッグトラッシュ（1回のトラッシュでたくさん捨てた）")]
        public string BigTrashName = "ビッグトラッシュ";
        [Tooltip("この枚数以上を1回で捨てたら付く。")]
        public int BigTrashMinCards = 3;
        public int BigTrashPoints = 20;

        [Header("ミラクルトラッシュ（手札を全部捨てて一気にあがった）")]
        public string MiracleTrashName = "ミラクルトラッシュ";
        [Tooltip("この枚数以上を1回で捨てて、そのターンにあがったら付く（このときビッグトラッシュは付けない）。")]
        public int MiracleTrashCards = 6;
        public int MiracleTrashPoints = 100;

        [Header("連続トラッシュ（自分の番で続けてトラッシュした）")]
        public string StreakName = "連続トラッシュ";
        [Tooltip("自分の番でこの回数続けてトラッシュしたら付く（付いたら数え直す。ノートラッシュの番でも数え直す）。")]
        public int StreakTurns = 3;
        public int StreakPoints = 30;

        /// <summary>人数と順位（1始まり）から、順位の得点を返す。表にない人数・順位は 0。</summary>
        public int GetRankPoints(int playerCount, int rank)
        {
            int[] table;
            switch (playerCount)
            {
                case 2: table = RankPoints2; break;
                case 3: table = RankPoints3; break;
                case 4: table = RankPoints4; break;
                default: return 0;
            }
            if (table == null || rank < 1 || rank > table.Length)
            {
                return 0;
            }
            return table[rank - 1];
        }
    }
}
