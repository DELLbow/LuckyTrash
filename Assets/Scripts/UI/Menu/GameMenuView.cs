using System;
using System.Collections;
using LuckyTrash.UI.Rules;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LuckyTrash.UI.Menu
{
    /// <summary>
    /// 試合中のメニュー。画面左下の小さな丸いボタン（≡）、または Esc キー（Android の戻るボタンも Esc として届く）で開閉する。
    /// 開いている間はゲームを一時停止（Time.timeScale = 0）し、続ける／ルールを見る／タイトルへ戻る／ゲームを終了 を選べる。
    /// 一時停止は Time.timeScale で行う：既存の演出は Time.deltaTime・WaitForSeconds・Time.time で動いているので、
    /// その場で止まり、再開すると止まったところから続く。メニュー自体のアニメーションは unscaled な時間で動く。
    /// 順位確定後の演出と取引明細書の間は <see cref="SetAvailable"/>(false) でボタンを隠し、Esc も受け付けない。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class GameMenuView : MonoBehaviour
    {
        private enum Page
        {
            Closed,
            Menu,
            Rules
        }

        [Header("素材")]
        [SerializeField] private Sprite _roundedSprite;
        [Tooltip("円（UI/Skin/Knob）。メニューボタンに使う。")]
        [SerializeField] private Sprite _circleSprite;
        [Tooltip("日本語の文字のフォント（MSGothic SDF）。")]
        [SerializeField] private TMP_FontAsset _textFont;
        [Tooltip("英字のフォント（LiberationSans SDF - Fallback）。")]
        [SerializeField] private TMP_FontAsset _symbolFont;
        [Tooltip("ルール説明の文章（Assets/Settings/RulesText.asset。タイトル画面と同じもの）。")]
        [SerializeField] private RulesContent _rules;
        [Tooltip("「タイトルへ戻る」「ゲームを終了」の確認。")]
        [SerializeField] private ConfirmDialogView _confirm;

        [Header("メニューボタン（Canvasの参照解像度基準の単位）")]
        [Tooltip("ボタンの直径（1080pで90px以上。右下のカジノチップより小さく）。")]
        [SerializeField] private float _buttonDiameter = 96f;
        [Tooltip("画面の左端・下端からの余白。")]
        [SerializeField] private Vector2 _buttonMargin = new Vector2(32f, 32f);
        [Tooltip("ボタンの地の色（黒）。")]
        [SerializeField] private Color _buttonColor = new Color(0.07f, 0.07f, 0.08f, 0.92f);
        [Tooltip("ボタンの細い縁取りと ≡ の色（ルーレットの縁と同じ金）。")]
        [SerializeField] private Color _buttonGoldColor = new Color(0.83f, 0.66f, 0.18f);

        [Header("パネル")]
        [SerializeField] private Vector2 _menuPaperSize = new Vector2(560f, 560f);
        [SerializeField] private Vector2 _rulesPaperSize = new Vector2(1200f, 780f);
        [SerializeField] private Vector2 _menuButtonSize = new Vector2(380f, 76f);
        [Range(0f, 1f)] [SerializeField] private float _backdropAlpha = 0.55f;
        [Tooltip("「続ける」のボタンの色（赤いインク）。")]
        [SerializeField] private Color _primaryInk = new Color(0.72f, 0.09f, 0.10f);
        [Tooltip("その他のボタンの色（墨色）。")]
        [SerializeField] private Color _ink = new Color(0.12f, 0.12f, 0.16f);
        [Tooltip("ルール説明の文字の大きさ。")]
        [SerializeField] private float _rulesFontSize = 28f;
        [Tooltip("パネルの表示・非表示の時間（秒。一時停止中でも動く）。")]
        [SerializeField] private float _animSeconds = 0.15f;

        [Header("文言")]
        [SerializeField] private string _returnConfirmMessage = "タイトルに戻ると、今の試合と通算の得点は消えます。よろしいですか？";
        [SerializeField] private string _quitConfirmMessage = "ゲームを終了しますか？";

        private RectTransform _button;
        private RectTransform _panelRoot;
        private Page _page = Page.Closed;
        private bool _available = true;
        private bool _pausedByMenu;
        private float _timeScaleBeforePause = 1f;
        private Action _returnToTitle;

        /// <summary>メニューが開いていて、ゲームが一時停止中か。</summary>
        public bool IsOpen => _page != Page.Closed;

        /// <summary>「タイトルへ戻る」で実行する処理（GameController から ResultNavigation.ReselectPlayers を渡す）。</summary>
        public void Initialize(Action returnToTitle)
        {
            _returnToTitle = returnToTitle;
        }

        /// <summary>メニューボタンを出す／隠す（順位確定後の演出と明細書の間は隠す）。隠している間は Esc でも開かない。</summary>
        public void SetAvailable(bool available)
        {
            _available = available;
            if (!available && IsOpen)
            {
                CloseMenu();
            }
            if (_button != null)
            {
                _button.gameObject.SetActive(available);
            }
        }

        private void Awake()
        {
            BuildButton();
        }

        private void OnDisable()
        {
            ResumeIfPaused();
        }

        private void OnDestroy()
        {
            ResumeIfPaused();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
            {
                return;
            }
            // Esc（Android の戻るボタン）は1段ずつ戻る：確認 → ルール → メニュー → 閉じる。閉じているときは開く。
            if (_confirm != null && _confirm.IsOpen)
            {
                _confirm.Cancel();
            }
            else if (_page == Page.Rules)
            {
                ShowMenuPage();
            }
            else if (_page == Page.Menu)
            {
                CloseMenu();
            }
            else if (_available)
            {
                OpenMenu();
            }
        }

        public void OpenMenu()
        {
            if (!_available || IsOpen)
            {
                return;
            }
            if (!_pausedByMenu)
            {
                _timeScaleBeforePause = Time.timeScale > 0f ? Time.timeScale : 1f;
                Time.timeScale = 0f;
                _pausedByMenu = true;
            }
            ShowMenuPage();
        }

        public void CloseMenu()
        {
            if (_confirm != null)
            {
                _confirm.Close();
            }
            DestroyPanel();
            _page = Page.Closed;
            ResumeIfPaused();
        }

        private void ResumeIfPaused()
        {
            if (_pausedByMenu)
            {
                Time.timeScale = _timeScaleBeforePause;
                _pausedByMenu = false;
            }
        }

        // ------------------------------------------------------------------
        // ページ
        // ------------------------------------------------------------------

        private void ShowMenuPage()
        {
            DestroyPanel();
            _page = Page.Menu;
            var paper = BuildPanel(_menuPaperSize);

            var title = MenuUiKit.CreateText("Title", paper, _textFont, "メニュー", 40f, MenuUiKit.NumberColor, TextAlignmentOptions.Center);
            title.fontStyle = FontStyles.Bold;
            title.rectTransform.sizeDelta = new Vector2(_menuPaperSize.x - 80f, 56f);
            title.rectTransform.anchoredPosition = new Vector2(0f, _menuPaperSize.y / 2f - 92f);

            float top = _menuPaperSize.y / 2f - 170f;
            float step = _menuButtonSize.y + 22f;
            MenuUiKit.CreateInkButton(paper, "続ける", _menuButtonSize, new Vector2(0f, top), _textFont, _roundedSprite, _primaryInk, true, CloseMenu);
            MenuUiKit.CreateInkButton(paper, "ルールを見る", _menuButtonSize, new Vector2(0f, top - step), _textFont, _roundedSprite, _ink, false, ShowRulesPage);
            MenuUiKit.CreateInkButton(paper, "タイトルへ戻る", _menuButtonSize, new Vector2(0f, top - step * 2f), _textFont, _roundedSprite, _ink, false, ConfirmReturnToTitle);
            MenuUiKit.CreateInkButton(paper, "ゲームを終了", _menuButtonSize, new Vector2(0f, top - step * 3f), _textFont, _roundedSprite, _ink, false, ConfirmQuit);
        }

        private void ShowRulesPage()
        {
            DestroyPanel();
            _page = Page.Rules;
            var paper = BuildPanel(_rulesPaperSize);

            var area = MenuUiKit.CreateRect("RulesArea", paper, new Vector2(_rulesPaperSize.x - 100f, _rulesPaperSize.y - 190f));
            area.anchoredPosition = new Vector2(0f, 12f);
            RulesScrollView.Build(area, _rules, _textFont, _rulesFontSize, MenuUiKit.NumberColor);

            MenuUiKit.CreateInkButton(paper, "閉じる", new Vector2(260f, 70f), new Vector2(0f, -_rulesPaperSize.y / 2f + 56f),
                _textFont, _roundedSprite, _ink, false, ShowMenuPage);
        }

        private void ConfirmReturnToTitle()
        {
            if (_confirm == null)
            {
                ReturnToTitle();
                return;
            }
            _confirm.Show(_returnConfirmMessage, "戻る", "やめる", ReturnToTitle, null);
        }

        private void ConfirmQuit()
        {
            if (_confirm == null)
            {
                AppQuit.Quit();
                return;
            }
            _confirm.Show(_quitConfirmMessage, "終了する", "やめる", AppQuit.Quit, null);
        }

        private void ReturnToTitle()
        {
            // シーンを移る前に、一時停止を必ず解く（次のシーンが止まったまま始まらないように）。
            CloseMenu();
            Time.timeScale = 1f;
            _returnToTitle?.Invoke();
        }

        // ------------------------------------------------------------------
        // 組み立て
        // ------------------------------------------------------------------

        private RectTransform BuildPanel(Vector2 paperSize)
        {
            _panelRoot = MenuUiKit.CreateRect("MenuPanel", transform, Vector2.zero);
            MenuUiKit.Stretch(_panelRoot);
            // メニューボタンより手前、確認のパネルより後ろ（確認はこの上に出る）。
            _panelRoot.SetAsLastSibling();
            if (_confirm != null && _confirm.transform.parent == transform)
            {
                _confirm.transform.SetAsLastSibling();
            }
            var group = _panelRoot.gameObject.AddComponent<CanvasGroup>();
            MenuUiKit.CreateBackdrop(_panelRoot, _backdropAlpha);
            var paper = MenuUiKit.CreatePaper(_panelRoot, paperSize, _roundedSprite, _symbolFont);
            StartCoroutine(Appear(paper, group));
            return paper;
        }

        private void DestroyPanel()
        {
            if (_panelRoot != null)
            {
                Destroy(_panelRoot.gameObject);
                _panelRoot = null;
            }
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
                paper.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, eased);
                yield return null;
            }
            if (paper != null)
            {
                group.alpha = 1f;
                paper.localScale = Vector3.one;
            }
        }

        /// <summary>左下の小さな丸いボタン（黒地に金の細い縁取り、≡ のアイコン）。</summary>
        private void BuildButton()
        {
            float d = _buttonDiameter;
            var root = new GameObject("MenuButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            _button = (RectTransform)root.transform;
            _button.SetParent(transform, false);
            _button.anchorMin = _button.anchorMax = Vector2.zero;
            _button.pivot = new Vector2(0.5f, 0.5f);
            _button.sizeDelta = Vector2.one * d;
            _button.anchoredPosition = _buttonMargin + Vector2.one * d / 2f;

            var ring = root.GetComponent<Image>();
            ring.sprite = _circleSprite;
            ring.color = _buttonGoldColor;
            ring.raycastTarget = true;
            var body = MenuUiKit.CreateImage("Body", _button, null, _buttonColor);
            body.sprite = _circleSprite;
            body.type = Image.Type.Simple;
            body.rectTransform.sizeDelta = Vector2.one * (d - 5f);

            // ≡（金の細い横棒3本）。
            for (int i = 0; i < 3; i++)
            {
                var bar = MenuUiKit.CreateImage($"Bar_{i}", _button, null, _buttonGoldColor);
                bar.rectTransform.sizeDelta = new Vector2(d * 0.40f, Mathf.Max(3f, d * 0.055f));
                bar.rectTransform.anchoredPosition = new Vector2(0f, (1 - i) * d * 0.15f);
            }

            var button = root.GetComponent<Button>();
            var colors = button.colors;
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            button.colors = colors;
            button.onClick.AddListener(OpenMenu);
        }
    }
}
