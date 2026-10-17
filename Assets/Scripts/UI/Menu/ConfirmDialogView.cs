using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LuckyTrash.UI.Menu
{
    /// <summary>
    /// 「よろしいですか？」の確認（アイボリーの紙に、文言と2つのボタン）。試合中のメニューとタイトル画面の両方で使う。
    /// 表示・非表示のアニメーションは一時停止中（Time.timeScale = 0）でも動く。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class ConfirmDialogView : MonoBehaviour
    {
        [Header("素材")]
        [SerializeField] private Sprite _roundedSprite;
        [Tooltip("日本語の文字のフォント（MSGothic SDF）。")]
        [SerializeField] private TMP_FontAsset _textFont;
        [Tooltip("英字のフォント（LiberationSans SDF - Fallback）。")]
        [SerializeField] private TMP_FontAsset _symbolFont;

        [Header("見た目")]
        [SerializeField] private Vector2 _paperSize = new Vector2(760f, 340f);
        [Range(0f, 1f)] [SerializeField] private float _backdropAlpha = 0.5f;
        [Tooltip("実行する側のボタン（「戻る」「終了する」）の色（赤いインク）。")]
        [SerializeField] private Color _confirmInk = new Color(0.72f, 0.09f, 0.10f);
        [Tooltip("取りやめる側のボタン（「やめる」）の色（墨色）。")]
        [SerializeField] private Color _cancelInk = new Color(0.12f, 0.12f, 0.16f);
        [SerializeField] private float _animSeconds = 0.15f;

        private RectTransform _root;
        private Action _onCancel;

        public bool IsOpen => _root != null;

        /// <summary>確認を出す。どちらかを押すと閉じてから、それぞれの処理を呼ぶ。</summary>
        public void Show(string message, string confirmLabel, string cancelLabel, Action onConfirm, Action onCancel)
        {
            Close();
            _onCancel = onCancel;
            _root = MenuUiKit.CreateRect("Confirm", transform, Vector2.zero);
            MenuUiKit.Stretch(_root);
            var group = _root.gameObject.AddComponent<CanvasGroup>();
            MenuUiKit.CreateBackdrop(_root, _backdropAlpha);
            var paper = MenuUiKit.CreatePaper(_root, _paperSize, _roundedSprite, _symbolFont);

            var text = MenuUiKit.CreateText("Message", paper, _textFont, message, 32f, MenuUiKit.NumberColor, TextAlignmentOptions.Center);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.enableAutoSizing = true;
            text.fontSizeMin = 20f;
            text.fontSizeMax = 32f;
            text.rectTransform.sizeDelta = new Vector2(_paperSize.x - 100f, _paperSize.y * 0.42f);
            text.rectTransform.anchoredPosition = new Vector2(0f, 22f);

            var buttonSize = new Vector2(240f, 72f);
            float y = -_paperSize.y / 2f + 62f;
            MenuUiKit.CreateInkButton(paper, cancelLabel, buttonSize, new Vector2(-140f, y), _textFont, _roundedSprite, _cancelInk, false,
                () => { Close(); onCancel?.Invoke(); });
            MenuUiKit.CreateInkButton(paper, confirmLabel, buttonSize, new Vector2(140f, y), _textFont, _roundedSprite, _confirmInk, true,
                () => { Close(); onConfirm?.Invoke(); });

            StartCoroutine(Appear(paper, group));
        }

        /// <summary>取りやめる（Esc・戻るボタンで閉じるとき用）。</summary>
        public void Cancel()
        {
            if (!IsOpen)
            {
                return;
            }
            var onCancel = _onCancel;
            Close();
            onCancel?.Invoke();
        }

        public void Close()
        {
            if (_root != null)
            {
                Destroy(_root.gameObject);
                _root = null;
            }
            _onCancel = null;
        }

        private IEnumerator Appear(RectTransform paper, CanvasGroup group)
        {
            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, _animSeconds);
            while (elapsed < duration && paper != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - (1f - t) * (1f - t);
                group.alpha = eased;
                paper.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, eased);
                yield return null;
            }
            if (paper != null)
            {
                group.alpha = 1f;
                paper.localScale = Vector3.one;
            }
        }
    }
}
