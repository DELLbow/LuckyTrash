using System.Collections.Generic;
using LuckyTrash.UI.Rules;
using LuckyTrash.UI.Scoring;
using NUnit.Framework;
using UnityEngine;

namespace LuckyTrash.UI.Tests
{
    public class RulesTextFormatterTests
    {
        [Test]
        public void Heading_IsBoldAndLarger()
        {
            Assert.AreEqual("<size=135%><b>題名</b></size>", RulesTextFormatter.Format("# 題名", null));
        }

        [Test]
        public void SubHeading_IsBoldWithSpaceBefore()
        {
            string result = RulesTextFormatter.Format("## 遊び方", null);
            StringAssert.Contains("<b>遊び方</b>", result);
            StringAssert.StartsWith("<line-height=150%>", result);
        }

        [Test]
        public void Bullet_HasHangingIndent()
        {
            Assert.AreEqual("・<indent=1.2em>項目</indent>", RulesTextFormatter.Format("- 項目", null));
        }

        [Test]
        public void NestedBullet_IsIndentedFurther()
        {
            Assert.AreEqual("<margin-left=1.5em>-<indent=1.2em>例</indent></margin>", RulesTextFormatter.Format("  - 例", null));
        }

        [Test]
        public void PlainLine_IsUnchanged()
        {
            Assert.AreEqual("本文です。", RulesTextFormatter.Format("本文です。", null));
        }

        [Test]
        public void RedSuits_AreColored_BlackSuitsAreNot()
        {
            string result = RulesTextFormatter.Format("♥と♦と♠と♣", null);
            StringAssert.Contains($"<color={RulesTextFormatter.RedSuitColor}>♥</color>", result);
            StringAssert.Contains($"<color={RulesTextFormatter.RedSuitColor}>♦</color>", result);
            StringAssert.Contains("と♠と♣", result);
        }

        [Test]
        public void Placeholders_AreReplaced_UnknownOnesAreKept()
        {
            var values = new Dictionary<string, string> { ["big_points"] = "20" };
            Assert.AreEqual("+20点 {unknown}", RulesTextFormatter.Format("+{big_points}点 {unknown}", values));
        }

        [Test]
        public void DefaultRules_UseScoreRulesValues()
        {
            var content = ScriptableObject.CreateInstance<RulesContent>();
            try
            {
                string text = content.BuildRichText();
                var rules = new ScoreRules();
                StringAssert.Contains($"1位{rules.GetRankPoints(4, 1)}点、2位{rules.GetRankPoints(4, 2)}点、3位{rules.GetRankPoints(4, 3)}点、4位{rules.GetRankPoints(4, 4)}点", text);
                StringAssert.Contains($"{rules.BigTrashMinCards}枚以上トラッシュ　+{rules.BigTrashPoints}点", text);
                StringAssert.Contains($"{rules.StreakTurns}回続けてトラッシュ　+{rules.StreakPoints}点", text);
                StringAssert.Contains($"手札{rules.MiracleTrashCards}枚を全部トラッシュ　+{rules.MiracleTrashPoints}点", text);
                StringAssert.DoesNotContain("{", text);
            }
            finally
            {
                Object.DestroyImmediate(content);
            }
        }
    }
}
