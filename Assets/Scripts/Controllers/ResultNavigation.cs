using LuckyTrash.UI.Scoring;
using UnityEngine.SceneManagement;

namespace LuckyTrash.Controllers
{
    /// <summary>
    /// ゲーム終了後の「もう一度」「人数を選び直す」の処理。ResultScene の ResultSceneController と、GameScene の取引明細書の
    /// ボタンの両方から同じ処理を呼ぶためにまとめたもの。
    /// </summary>
    public static class ResultNavigation
    {
        private const string GameSceneName = "GameScene";
        private const string TitleSceneName = "TitleScene";

        /// <summary>
        /// 「もう一度」：GameManager にクイック再開を予約したうえで GameScene を読み込み直す（人数選択をスキップして
        /// 前回と同じ人数で自動開始される。通算の得点は引き継ぐ）。
        /// </summary>
        public static void PlayAgain()
        {
            GameManager.Instance.RequestQuickRestart();
            SceneManager.LoadScene(GameSceneName);
        }

        /// <summary>
        /// 「人数を選び直す」：クイック再開の予約をクリアし、TitleScene へ戻る（モード選択→人数選択ポップアップから選び直す）。
        /// 通算の得点はリセットする。
        /// </summary>
        public static void ReselectPlayers()
        {
            GameManager.Instance.ClearQuickRestart();
            ScoreSession.Book.ResetSession();
            SceneManager.LoadScene(TitleSceneName);
        }
    }
}
