using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace LuckyTrash.UI.Rules
{
    /// <summary>
    /// ルール説明の元の文章（かんたんな書式つきのテキスト）を、TextMeshPro のリッチテキストに変換する純粋関数。
    /// 書式（行の先頭）：
    /// 「# 」大見出し、「## 」見出し、「- 」箇条書き、「  - 」（空白2つ以上＋-）一段下げた箇条書き、それ以外は本文。
    /// 「{名前}」は values の値に差し替える（得点の数値を ScoreRules.asset から差し込むため。見つからなければそのまま残す）。
    /// ♥・♦ は赤で表示する。
    /// </summary>
    public static class RulesTextFormatter
    {
        /// <summary>♥・♦ の赤（カードの赤と同じくらいの濃さ）。</summary>
        public const string RedSuitColor = "#C8202A";

        private static readonly Regex Placeholder = new Regex(@"\{([A-Za-z0-9_]+)\}");

        public static string Format(string source, IReadOnlyDictionary<string, string> values)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            string text = Placeholder.Replace(source.Replace("\r\n", "\n"), m =>
                values != null && values.TryGetValue(m.Groups[1].Value, out var v) ? v : m.Value);

            var sb = new StringBuilder();
            var lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string trimmed = line.TrimStart(' ', '\t', '　');
                int indent = line.Length - trimmed.Length;
                string body = ColorSuits(trimmed);

                if (trimmed.StartsWith("## "))
                {
                    // 見出しの前は少し空ける。
                    sb.Append("<line-height=150%>\n</line-height>");
                    sb.Append("<size=115%><b>").Append(ColorSuits(trimmed.Substring(3))).Append("</b></size>");
                }
                else if (trimmed.StartsWith("# "))
                {
                    sb.Append("<size=135%><b>").Append(ColorSuits(trimmed.Substring(2))).Append("</b></size>");
                }
                else if (trimmed.StartsWith("- "))
                {
                    string item = ColorSuits(trimmed.Substring(2));
                    if (indent >= 2)
                    {
                        // 一段下げた箇条書き（折り返した行も、記号の後ろにそろえる）。
                        sb.Append("<margin-left=1.5em>-<indent=1.2em>").Append(item).Append("</indent></margin>");
                    }
                    else
                    {
                        sb.Append("・<indent=1.2em>").Append(item).Append("</indent>");
                    }
                }
                else
                {
                    sb.Append(body);
                }

                if (i < lines.Length - 1)
                {
                    sb.Append('\n');
                }
            }
            return sb.ToString();
        }

        private static string ColorSuits(string text)
        {
            return text
                .Replace("♥", $"<color={RedSuitColor}>♥</color>")
                .Replace("♦", $"<color={RedSuitColor}>♦</color>");
        }
    }
}
