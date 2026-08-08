using System;
using System.Collections.Generic;

namespace LuckyTrash.Game
{
    /// <summary>
    /// 全6面のルーレットで RouletteCategory を抽選するクラス。
    /// 面の内訳: Number 3面 / Suit 2面 / Color 1面。
    /// </summary>
    public class RouletteSelector
    {
        /// <summary>
        /// ルーレットの各面に割り当てられたカテゴリ（全6面）。
        /// Number 3面 / Suit 2面 / Color 1面。
        /// </summary>
        private static readonly RouletteCategory[] Faces =
        {
            RouletteCategory.Number,
            RouletteCategory.Number,
            RouletteCategory.Number,
            RouletteCategory.Suit,
            RouletteCategory.Suit,
            RouletteCategory.Color
        };

        /// <summary>
        /// ルーレットの面構成（テスト・検証用に公開）。
        /// </summary>
        public static IReadOnlyList<RouletteCategory> AllFaces => Faces;

        private readonly Random _random;

        /// <summary>
        /// ルーレット抽選器を生成する。
        /// </summary>
        /// <param name="random">
        /// 抽選に使用する乱数生成器。省略時は既定の System.Random を使用する。
        /// テストで結果を固定したい場合はシード付きの Random を渡す。
        /// </param>
        public RouletteSelector(Random random = null)
        {
            _random = random ?? new Random();
        }

        /// <summary>
        /// ルーレットを1回まわし、6面の中から等確率で1面を選んで、その面の RouletteCategory を返す。
        /// （面の内訳が Number 3 / Suit 2 / Color 1 のため、結果として Number:Suit:Color は 3:2:1 の重みで出現する）
        /// </summary>
        public RouletteCategory Spin()
        {
            int index = _random.Next(Faces.Length);
            return Faces[index];
        }
    }
}
