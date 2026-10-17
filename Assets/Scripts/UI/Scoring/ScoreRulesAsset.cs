using UnityEngine;

namespace LuckyTrash.UI.Scoring
{
    /// <summary>得点のルールを Inspector で調整するためのアセット（Assets/Settings/ScoreRules.asset）。</summary>
    [CreateAssetMenu(fileName = "ScoreRules", menuName = "LuckyTrash/Score Rules")]
    public class ScoreRulesAsset : ScriptableObject
    {
        [SerializeField] private ScoreRules _rules = new ScoreRules();

        public ScoreRules Rules => _rules;
    }
}
