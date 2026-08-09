using System.Text;
using LuckyTrash.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LuckyTrash.Controllers
{
    /// <summary>
    /// ResultScene に配置し、直近ゲームの最終順位を表示するコントローラー。
    /// GameManager に記録された最終順位データ（座席インデックスのリスト、1位から順）を読み込んで、
    /// 表示名（下座席=人間はTitleSceneで保存された名前、他の座席=CPU対戦の「CPU 1」等）で
    /// 順位を表示する。座席インデックスから表示名を復元する規則は GameSetup と同じ
    /// （SeatIndex 0 が人間、それ以外は座席順に「CPU 座席番号」）で、GameState 自体は
    /// シーン遷移で失われるため GameManager には座席インデックスのみを保持させ、
    /// ここではその規則から表示名を再構築する。
    /// 「もう一度プレイ」は GameManager にクイック再開を予約したうえで GameScene へ遷移し
    /// （人数選択をスキップして前回と同じ人数で自動開始される）、
    /// 「人数選択やり直し」はクイック再開の予約を明示的にクリアしたうえで TitleScene へ遷移する
    /// （TitleScene のモード選択→人数選択ポップアップから選び直す）。
    /// </summary>
    public class ResultSceneController : MonoBehaviour
    {
        private const string GameSceneName = "GameScene";
        private const string TitleSceneName = "TitleScene";
        private const int HumanSeatIndex = 0;

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
                sb.Append('\n').Append(rank).Append("位: ").Append(GetDisplayName(rankingSeatIndices[i]));
            }

            _rankingText.text = sb.ToString();
        }

        /// <summary>
        /// 座席インデックスから表示名を復元する。SeatIndex 0（下座席）は人間としてTitleSceneの
        /// 保存名を、それ以外はCPU対戦の座席番号に対応する「CPU N」を返す（GameSetupと同じ規則）。
        /// </summary>
        private static string GetDisplayName(int seatIndex)
        {
            return seatIndex == HumanSeatIndex
                ? NameInputPopupView.LoadPlayerName()
                : $"CPU {seatIndex}";
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
