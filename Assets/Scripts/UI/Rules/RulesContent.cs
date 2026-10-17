using System.Collections.Generic;
using LuckyTrash.Game;
using LuckyTrash.UI.Scoring;
using UnityEngine;

namespace LuckyTrash.UI.Rules
{
    /// <summary>
    /// ルール説明の文章（Assets/Settings/RulesText.asset）。タイトル画面の「ルール確認」と、試合中のメニューの「ルールを見る」の
    /// 両方がここから読み込むので、書き換えるときはこの1か所だけでよい。
    /// 得点の数値は {rank4_1} などの差し込み記号で書き、ScoreRules.asset の値を差し込む（食い違わないように）。
    /// 書式は <see cref="RulesTextFormatter"/> を参照。
    /// </summary>
    [CreateAssetMenu(fileName = "RulesText", menuName = "LuckyTrash/Rules Text")]
    public class RulesContent : ScriptableObject
    {
        [Tooltip("得点の数値を差し込む元（Assets/Settings/ScoreRules.asset）。")]
        [SerializeField] private ScoreRulesAsset _scoreRules;

        [Tooltip("ルール説明の文章。行の先頭が「# 」大見出し、「## 」見出し、「- 」箇条書き、「  - 」一段下げた箇条書き。\n" +
            "差し込み記号：{hand} {rank4_1}〜{rank4_4} {big_cards} {big_points} {streak_turns} {streak_points} {miracle_cards} {miracle_points}")]
        [TextArea(20, 60)]
        [SerializeField] private string _source = DefaultSource;

        public const string DefaultSource =
@"# ラッキートラッシュのルール
自分とCPUの2〜4人で遊ぶカードゲームです。手札は1人{hand}枚です。
## 遊び方
- 自分の番では、「回す」でルーレットを回して、判定（数字・マーク・色）を決めます
- 次に「引く」で山札から1枚めくります。これが基準カードです
- 手札の中で、基準カードと判定が同じカードは、全部捨てられます（トラッシュ）
  - 判定が「数字」で基準カードが♥7なら、手札の7はすべてトラッシュ
  - 判定が「マーク」で基準カードが♥7なら、手札の♥はすべてトラッシュ
  - 判定が「色」で基準カードが♥7なら、手札の赤（♥と♦）はすべてトラッシュ
- 同じカードがなければ「ノートラッシュ」。次の人の番です
- 手札が0枚になった人から順位が決まります。最後に残った1人が最下位です
## 得点
- 順位の得点：4人なら1位{rank4_1}点、2位{rank4_2}点、3位{rank4_3}点、4位{rank4_4}点
- ボーナス
  - ビッグトラッシュ：1回で{big_cards}枚以上トラッシュ　+{big_points}点
  - 連続トラッシュ：自分の番で{streak_turns}回続けてトラッシュ　+{streak_points}点
  - ミラクルトラッシュ：1回で手札{miracle_cards}枚を全部トラッシュ　+{miracle_points}点
- 「もう一度」で続けて遊ぶと、得点が積み上がります。通算の得点で競いましょう";

        /// <summary>差し込み記号に入れる値（ScoreRules.asset と手札の枚数から作る）。</summary>
        public Dictionary<string, string> BuildValues()
        {
            var rules = _scoreRules != null ? _scoreRules.Rules : new ScoreRules();
            var values = new Dictionary<string, string>
            {
                ["hand"] = GameSetup.InitialHandSize.ToString(),
                ["big_cards"] = rules.BigTrashMinCards.ToString(),
                ["big_points"] = rules.BigTrashPoints.ToString(),
                ["streak_turns"] = rules.StreakTurns.ToString(),
                ["streak_points"] = rules.StreakPoints.ToString(),
                ["miracle_cards"] = rules.MiracleTrashCards.ToString(),
                ["miracle_points"] = rules.MiracleTrashPoints.ToString(),
            };
            for (int rank = 1; rank <= 4; rank++)
            {
                values[$"rank4_{rank}"] = rules.GetRankPoints(4, rank).ToString();
            }
            return values;
        }

        /// <summary>表示用のリッチテキスト。</summary>
        public string BuildRichText()
        {
            return RulesTextFormatter.Format(string.IsNullOrEmpty(_source) ? DefaultSource : _source, BuildValues());
        }
    }
}
