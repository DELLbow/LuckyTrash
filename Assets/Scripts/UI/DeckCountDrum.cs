using System;

namespace LuckyTrash.UI
{
    /// <summary>
    /// 山札の残り枚数をスロットのドラムのように切り替えるとき、各桁を何コマ回すかを決める純粋関数。
    /// 桁は上位から順に並べ、各桁の表示は 0〜9（最上位桁の 0 は空白として表示する想定）。
    /// 減るときは各桁を「減る向き」に、増えるときは「増える向き」に、変わる桁だけを最短のコマ数で回す。
    /// 増えるとき（山札の作り直し）は、勢いを出すために変わる桁へ全周 extraTurnsOnIncrease 回ぶんを足せる。
    /// </summary>
    public static class DeckCountDrum
    {
        /// <summary>
        /// 各桁の回転量（コマ数）を、上位の桁から順に返す。負の値は減る向き、正の値は増える向き、0は回さない。
        /// </summary>
        /// <param name="from">切り替え前の枚数（0以上）。</param>
        /// <param name="to">切り替え後の枚数（0以上）。</param>
        /// <param name="digitCount">表示する桁数（1以上）。</param>
        /// <param name="extraTurnsOnIncrease">増えるとき、変わる桁に追加で回す全周の回数（0以上）。</param>
        public static int[] GetSteps(int from, int to, int digitCount, int extraTurnsOnIncrease = 0)
        {
            if (from < 0 || to < 0)
            {
                throw new ArgumentOutOfRangeException(from < 0 ? nameof(from) : nameof(to), "Counts must be zero or greater.");
            }
            if (digitCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(digitCount), "At least one digit is required.");
            }

            var steps = new int[digitCount];
            for (int i = 0; i < digitCount; i++)
            {
                int place = digitCount - 1 - i;
                int fromDigit = GetDigit(from, place);
                int toDigit = GetDigit(to, place);
                if (fromDigit == toDigit)
                {
                    continue;
                }

                if (to < from)
                {
                    steps[i] = -Modulo(fromDigit - toDigit, 10);
                }
                else
                {
                    steps[i] = Modulo(toDigit - fromDigit, 10) + 10 * Math.Max(0, extraTurnsOnIncrease);
                }
            }
            return steps;
        }

        /// <summary>value の 10^place の位の数字（0〜9）。</summary>
        public static int GetDigit(int value, int place)
        {
            for (int i = 0; i < place; i++)
            {
                value /= 10;
            }
            return value % 10;
        }

        private static int Modulo(int value, int modulus)
        {
            int r = value % modulus;
            return r < 0 ? r + modulus : r;
        }
    }
}
