using UnityEngine;

namespace LuckyTrash.UI.Menu
{
    /// <summary>アプリを閉じる（エディタ上では再生を止める）。</summary>
    public static class AppQuit
    {
        public static void Quit()
        {
            // 一時停止中に終了しても、次に起動・再生したときに止まったままにならないように戻しておく。
            Time.timeScale = 1f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
