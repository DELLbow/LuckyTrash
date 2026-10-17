namespace LuckyTrash.UI.Scoring
{
    /// <summary>
    /// 通算の得点（<see cref="ScoreBook"/>）を、シーンの読み込み直し（「もう一度プレイ」で GameScene を読み込み直す）をまたいで
    /// 持っておくための入れ物。静的な値なので、シーンが変わっても残り、アプリを閉じると消える。
    /// リセットするかどうかは、勝負を始める側（GameController）が <see cref="ScoreBook.BeginMatch"/> の引数で決める。
    /// </summary>
    public static class ScoreSession
    {
        private static ScoreBook _book;

        /// <summary>今の通算の得点。初めて使うときに既定のルールで作る（勝負の始めにルールを差し替える）。</summary>
        public static ScoreBook Book => _book ?? (_book = new ScoreBook(new ScoreRules()));
    }
}
