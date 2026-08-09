using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LuckyTrash.Controllers
{
    /// <summary>
    /// ResultScene に配置し、直近ゲームの最終順位を表示するコントローラー。
    /// GameManager に記録された最終順位データ（座席インデックスのリスト、1位から順）を読み込んで、
    /// GameScene での表示と同等のフォーマット（「1位: Player X」〜）で表示する。
    /// 「もう一度プレイ」は GameManager にクイック再開を予約したうえで GameScene へ遷移し
    /// （人数選択をスキップして前回と同じ人数で自動開始される）、
    /// 「人数選択やり直し」はクイック再開の予約を明示的にクリアしたうえで TitleScene へ遷移する
    /// （TitleScene のモード選択→人数選択ポップアップから選び直す）。
    /// </summary>
    public class ResultSceneController : MonoBehaviour
    {
        private const string GameSceneName = "GameScene";
        private const string TitleSceneName = "TitleScene";

        [SerializeField] private TMP_Text _rankingText;
        [SerializeField] private Button _playAgainButton;
        [SerializeField] private Button _restartWithSelectionButton;

        private void Start()
        {
            DisplayFinalRanking();

            if (_playAgainButton != null)
            {
                _playAgainButton.onClick.AddListener(OnPlayAgainClicked);
            }

            if (_restartWithSelectionButton != null)
            {
                _restartWithSelectionButton.onClick.AddListener(OnRestartWithSelectionClicked);
            }
        }

        private void DisplayFinalRanking()
        {
            if (_rankingText == null)
            {
                return;
            }

            var rankingSeatIndices = GameManager.Instance.LastFinalRankingSeatIndices;
            if (rankingSeatIndices == null || rankingSeatIndices.Count == 0)
            {
                _rankingText.text = "結果データがありません。";
                return;
            }

            var sb = new StringBuilder("=== ゲーム終了 ===");
            for (int i = 0; i < rankingSeatIndices.Count; i++)
            {
                int rank = i + 1;
                sb.Append('\n').Append(rank).Append("位: Player ").Append(rankingSeatIndices[i]);
            }

            _rankingText.text = sb.ToString();
        }

        /// <summary>
        /// 「もう一度プレイ」：人数選択を経ずに、前回と同じ人数でそのままゲームを開始する。
        /// </summary>
        private void OnPlayAgainClicked()
        {
            GameManager.Instance.RequestQuickRestart();
            SceneManager.LoadScene(GameSceneName);
        }

        /// <summary>
        /// 「人数選択やり直し」：クイック再開の予約をクリアし、TitleScene へ戻って
        /// モード選択→人数選択ポップアップから選び直す。
        /// </summary>
        private void OnRestartWithSelectionClicked()
        {
            GameManager.Instance.ClearQuickRestart();
            SceneManager.LoadScene(TitleSceneName);
        }
    }
}
