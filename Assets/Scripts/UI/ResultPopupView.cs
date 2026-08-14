using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LuckyTrash.UI
{
    /// <summary>
    /// 判定結果（捨てたカードの枚数）を、画面右下外から弧を描いて中央へスライドインし、
    /// しばらく表示した後、同じ軌道を逆再生して右下外へ退場するポップアップ。
    /// </summary>
    public class ResultPopupView : MonoBehaviour
    {
        [SerializeField] private RectTransform _rectTransform;
        [SerializeField] private TMP_Text _label;
        [SerializeField] private Image _background;

        [Tooltip("登場〜退場開始までの待機秒数。")]
        [SerializeField] private float _resultPopupDisplaySeconds = 2f;

        [SerializeField] private float _transitionDurationSeconds = 0.5f;

        [Tooltip("定位置（画面中央）から見た、登場開始・退場先の画面外オフセット（右下方向）。")]
        [SerializeField] private Vector2 _offscreenOffset = new Vector2(900f, -700f);

        [SerializeField] private Color _trashColor = new Color(1f, 0.62f, 0.2f, 0.88f);
        [SerializeField] private Color _noTrashColor = new Color(0.42f, 0.42f, 0.45f, 0.88f);

        [Tooltip("ShowMessage（あがり演出等での汎用メッセージ表示）で使う背景色。")]
        [SerializeField] private Color _messageColor = new Color(1f, 0.85f, 0.2f, 0.92f);

        private Vector2 _restingAnchoredPosition;
        private Vector2 _offscreenAnchoredPosition;
        private bool _initialized;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            if (_rectTransform == null)
            {
                _rectTransform = (RectTransform)transform;
            }

            _restingAnchoredPosition = _rectTransform.anchoredPosition;
            _offscreenAnchoredPosition = _restingAnchoredPosition + _offscreenOffset;
            _initialized = true;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 捨てたカードの枚数を受け取り、ポップアップの登場→待機→退場までを一括で行うコルーチン。
        /// discardedCount が 0 の場合は「ノートラッシュ…」、1以上なら「N枚トラッシュ!」を表示する。
        /// </summary>
        public IEnumerator ShowResult(int discardedCount)
        {
            bool hasTrash = discardedCount > 0;
            string text = hasTrash ? $"{discardedCount}枚トラッシュ!" : "ノートラッシュ…";
            Color color = hasTrash ? _trashColor : _noTrashColor;
            yield return ShowInternal(text, color);
        }

        /// <summary>
        /// 任意のテキスト（例:「1位あがり!」）を <see cref="_messageColor"/> で表示する、
        /// <see cref="ShowResult"/> と同じ登場→待機→退場の演出を再生するコルーチン。
        /// あがり演出（カメラのズームインと合わせて使う想定）用に、既存のトラッシュ枚数表示ポップアップの
        /// 見た目・動きを流用するために追加した。既存の <see cref="ShowResult"/> の呼び出し側には
        /// 影響しない。連続で呼び出す場合、呼び出し元が前の呼び出しの完了（コルーチンのyield完了）を
        /// 待ってから呼び出すことで、2つのポップアップが重ならずに順番に再生される。
        /// </summary>
        public IEnumerator ShowMessage(string text)
        {
            yield return ShowInternal(text, _messageColor);
        }

        private IEnumerator ShowInternal(string text, Color color)
        {
            EnsureInitialized();

            if (_label != null)
            {
                _label.text = text;
            }

            if (_background != null)
            {
                _background.color = color;
            }

            gameObject.SetActive(true);
            _rectTransform.anchoredPosition = _offscreenAnchoredPosition;

            yield return MoveAlongArc(_offscreenAnchoredPosition, _restingAnchoredPosition, _transitionDurationSeconds);

            yield return new WaitForSeconds(_resultPopupDisplaySeconds);

            yield return MoveAlongArc(_restingAnchoredPosition, _offscreenAnchoredPosition, _transitionDurationSeconds);

            gameObject.SetActive(false);
        }

        /// <summary>
        /// from から to まで、進行方向に対して垂直な方向へ膨らむ二次ベジェ曲線（放物線状の弧）で
        /// RectTransform.anchoredPosition を補間するコルーチン。
        /// </summary>
        private IEnumerator MoveAlongArc(Vector2 from, Vector2 to, float duration)
        {
            Vector2 mid = (from + to) * 0.5f;
            Vector2 direction = (to - from);
            float distance = direction.magnitude;

            Vector2 control = mid;
            if (distance > 0.001f)
            {
                Vector2 normalizedDir = direction / distance;
                Vector2 perpendicular = new Vector2(-normalizedDir.y, normalizedDir.x);
                control = mid + perpendicular * (distance * 0.35f);
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 2f); // ease-out
                _rectTransform.anchoredPosition = QuadraticBezier(from, control, to, eased);
                yield return null;
            }

            _rectTransform.anchoredPosition = to;
        }

        private static Vector2 QuadraticBezier(Vector2 p0, Vector2 p1, Vector2 p2, float t)
        {
            float u = 1f - t;
            return (u * u * p0) + (2f * u * t * p1) + (t * t * p2);
        }
    }
}
