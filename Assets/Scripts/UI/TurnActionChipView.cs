using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LuckyTrash.UI
{
    /// <summary>
    /// 「ルーレットを回す」「1枚引く」を、画面右下の1枚のカジノチップ型ボタンとして見せるビュー。
    /// クリックの処理と押せる条件は既存の2つの Button（GameController の _spinRouletteButton / _drawCardButton）
    /// をそのまま使い、このビューは見た目だけを担当する。2つの Button はこのチップの子として同じ位置に重ね、
    /// 透明なタップ領域として使う。
    ///
    /// 毎フレーム2つの Button の状態（表示されていて、かつ押せるか）を見て、表示する面（回す／引く）を決める。
    /// どちらも押せないとき（CPUの手番、演出の途中など）はチップを隠し、タップも受け付けない。
    /// 「回す」から「引く」に変わるときは、チップがY軸まわりに裏返って面が切り替わる。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class TurnActionChipView : MonoBehaviour
    {
        private enum Face
        {
            None,
            Spin,
            Draw
        }

        [Header("参照（既存のボタン）")]
        [SerializeField] private Button _spinButton;
        [SerializeField] private Button _drawButton;

        [Header("素材")]
        [Tooltip("円（UI/Skin/Knob）。チップ・切れ込みの内側・アイコンの丸い切り抜きに使う。")]
        [SerializeField] private Sprite _circleSprite;
        [Tooltip("「回す」のアイコン。焼き込み済みのルーレット盤面（RouletteFace_Baked.png）を縮小して使う。")]
        [SerializeField] private Texture _rouletteIcon;
        [Tooltip("「引く」のアイコン。カードの裏面（Free Playing Cards Pack の BackColor_Blue）。")]
        [SerializeField] private Sprite _cardBackIcon;
        [SerializeField] private TMP_FontAsset _font;

        [Header("配置（Canvasの参照解像度基準の単位）")]
        [Tooltip("チップの直径（タップ領域）。1080pで140px以上が目安。")]
        [SerializeField] private float _diameter = 160f;
        [Tooltip("画面の右端・下端からの余白。")]
        [SerializeField] private Vector2 _screenMargin = new Vector2(28f, 28f);

        [Header("色")]
        [SerializeField] private Color _chipColor = new Color(0.72f, 0.09f, 0.10f);
        [SerializeField] private Color _notchColor = new Color(1f, 0.98f, 0.92f);
        [SerializeField] private Color _innerRingColor = new Color(1f, 0.96f, 0.86f);
        [SerializeField] private Color _labelColor = Color.white;
        [SerializeField] private Color _labelOutlineColor = new Color(0.25f, 0.02f, 0.02f);
        [Tooltip("待機中に縁で光る色（ルーレットの金にそろえる）。")]
        [SerializeField] private Color _glowColor = new Color(1f, 0.82f, 0.30f);

        [Header("文字")]
        [SerializeField] private string _spinLabel = "回す";
        [SerializeField] private string _drawLabel = "引く";
        [SerializeField] private float _labelSize = 34f;

        [Header("待機中の脈動")]
        [Tooltip("拡大縮小と縁の光の1周期（秒）。")]
        [SerializeField] private float _pulsePeriod = 1.2f;
        [Tooltip("拡大縮小の振れ幅（0.05なら1.00〜1.05倍）。")]
        [SerializeField] private float _pulseScale = 0.05f;
        [Tooltip("縁の光の最大の不透明度。")]
        [Range(0f, 1f)] [SerializeField] private float _pulseGlowAlpha = 0.75f;

        [Header("押したときの沈み込み")]
        [SerializeField] private float _pressedScale = 0.92f;
        [Tooltip("押す・離すときの変化にかける時間（秒）。")]
        [SerializeField] private float _pressDuration = 0.08f;

        [Header("切り替え")]
        [Tooltip("「回す」から「引く」に裏返る時間（秒）。")]
        [SerializeField] private float _flipDuration = 0.3f;
        [Tooltip("表示・非表示のフェード時間（秒）。")]
        [SerializeField] private float _fadeDuration = 0.12f;

        private CanvasGroup _group;
        private RectTransform _visual;
        private RectTransform _shadow;
        private Image _shadowImage;
        private Image _glow;
        private RawImage _rouletteImage;
        private GameObject _rouletteMask;
        private Image _cardImage;
        private TMP_Text _label;

        private Face _shownFace = Face.None;
        private Face _lastVisibleFace = Face.None;
        private float _alpha;
        private float _press;
        private bool _pressed;
        private float _flipElapsed = -1f;
        private float _pulseTime;

        /// <summary>押下中かどうか（<see cref="TurnActionChipPressRelay"/> から呼ばれる）。</summary>
        public void SetPressed(bool pressed)
        {
            _pressed = pressed && _shownFace != Face.None;
        }

        private void Awake()
        {
            Build();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            Face target = ResolveTargetFace();

            if (target != _shownFace)
            {
                OnFaceChanged(target);
            }

            // 表示・非表示（非表示中はタップも受け付けない）。
            float alphaTarget = _shownFace == Face.None ? 0f : 1f;
            _alpha = Mathf.MoveTowards(_alpha, alphaTarget, dt / Mathf.Max(0.01f, _fadeDuration));
            _group.alpha = _alpha;
            _group.blocksRaycasts = _shownFace != Face.None;
            if (_shownFace == Face.None)
            {
                _pressed = false;
            }

            // 押したときの沈み込み。
            _press = Mathf.MoveTowards(_press, _pressed ? 1f : 0f, dt / Mathf.Max(0.01f, _pressDuration));

            // 裏返り（Y軸回転）。半分まで回ったところで面を差し替える。
            float flipAngle = 0f;
            if (_flipElapsed >= 0f)
            {
                _flipElapsed += dt;
                float t = Mathf.Clamp01(_flipElapsed / Mathf.Max(0.01f, _flipDuration));
                if (t < 0.5f)
                {
                    flipAngle = Mathf.Lerp(0f, 90f, t * 2f);
                    ApplyFace(Face.Spin);
                }
                else
                {
                    flipAngle = Mathf.Lerp(-90f, 0f, (t - 0.5f) * 2f);
                    ApplyFace(Face.Draw);
                }
                if (t >= 1f)
                {
                    _flipElapsed = -1f;
                }
            }

            // 待機中の脈動（押下中・裏返り中は止める）。
            bool idle = _shownFace != Face.None && !_pressed && _flipElapsed < 0f;
            _pulseTime = idle ? _pulseTime + dt : 0f;
            float wave = idle ? 0.5f - 0.5f * Mathf.Cos(_pulseTime * Mathf.PI * 2f / Mathf.Max(0.1f, _pulsePeriod)) : 0f;

            float scale = (1f + _pulseScale * wave) * Mathf.Lerp(1f, _pressedScale, _press);
            _visual.localScale = Vector3.one * scale;
            _visual.localRotation = Quaternion.Euler(0f, flipAngle, 0f);

            var glow = _glowColor;
            glow.a = _pulseGlowAlpha * wave;
            _glow.color = glow;
            _glow.rectTransform.localScale = _visual.localScale;
            _glow.rectTransform.localRotation = _visual.localRotation;

            // 影は押すと小さく、薄くなる（チップが台に近づいて見える）。
            _shadow.anchoredPosition = Vector2.Lerp(new Vector2(4f, -7f), new Vector2(1.5f, -2.5f), _press);
            _shadowImage.color = new Color(0f, 0f, 0f, Mathf.Lerp(0.38f, 0.22f, _press));
            _shadow.localScale = Vector3.one * Mathf.Lerp(1f, _pressedScale, _press);
            _shadow.localRotation = _visual.localRotation;
        }

        private Face ResolveTargetFace()
        {
            if (_spinButton != null && _spinButton.gameObject.activeSelf && _spinButton.interactable)
            {
                return Face.Spin;
            }
            if (_drawButton != null && _drawButton.gameObject.activeSelf && _drawButton.interactable)
            {
                return Face.Draw;
            }
            return Face.None;
        }

        private void OnFaceChanged(Face target)
        {
            if (target == Face.Draw && _lastVisibleFace == Face.Spin)
            {
                // ルーレットの後のドロー: 「回す」の面から裏返って「引く」になる。
                _flipElapsed = 0f;
                ApplyFace(Face.Spin);
            }
            else if (target != Face.None)
            {
                _flipElapsed = -1f;
                ApplyFace(target);
            }

            if (target != Face.None)
            {
                _lastVisibleFace = target;
            }
            _shownFace = target;
        }

        private void ApplyFace(Face face)
        {
            bool spin = face != Face.Draw;
            _rouletteMask.SetActive(spin);
            _cardImage.gameObject.SetActive(!spin);
            _label.text = spin ? _spinLabel : _drawLabel;
        }

        // ------------------------------------------------------------------
        // 見た目の組み立て（新しい画像素材を使わず、Image・RawImage・TextMeshPro・Outlineで作る）
        // ------------------------------------------------------------------

        private void Build()
        {
            var rect = (RectTransform)transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.one * _diameter;
            rect.anchoredPosition = new Vector2(-_screenMargin.x - _diameter / 2f, _screenMargin.y + _diameter / 2f);

            _group = GetComponent<CanvasGroup>();
            if (_group == null)
            {
                _group = gameObject.AddComponent<CanvasGroup>();
            }
            _group.alpha = 0f;
            _group.blocksRaycasts = false;

            float d = _diameter;

            _shadowImage = CreateImage("Shadow", transform, _circleSprite, new Color(0f, 0f, 0f, 0.38f), d);
            _shadow = _shadowImage.rectTransform;

            _glow = CreateImage("Glow", transform, _circleSprite, new Color(_glowColor.r, _glowColor.g, _glowColor.b, 0f), d * 1.22f);

            _visual = new GameObject("Visual", typeof(RectTransform)).GetComponent<RectTransform>();
            _visual.SetParent(transform, false);
            _visual.sizeDelta = Vector2.one * d;

            CreateImage("Disc", _visual, _circleSprite, _chipColor, d);

            // 縁に沿って等間隔の白い切れ込み（8か所）。
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f;
                var notch = CreateImage($"Notch_{i}", _visual, null, _notchColor, 0f);
                var r = notch.rectTransform;
                // 外周の帯（内側の輪より外）だけに収まる長さにする。
                r.sizeDelta = new Vector2(d * 0.09f, d * 0.10f);
                r.localRotation = Quaternion.Euler(0f, 0f, -angle);
                r.anchoredPosition = Quaternion.Euler(0f, 0f, -angle) * new Vector3(0f, d * 0.44f, 0f);
            }

            // 内側の一回り小さい円（アイボリーの細い輪）と、その内側の地。
            CreateImage("InnerRing", _visual, _circleSprite, _innerRingColor, d * 0.74f);
            CreateImage("InnerField", _visual, _circleSprite, Color.Lerp(_chipColor, Color.black, 0.12f), d * 0.69f);

            // アイコン（丸く切り抜く）。
            var mask = CreateImage("IconMask", _visual, _circleSprite, Color.white, d * 0.40f);
            mask.rectTransform.anchoredPosition = new Vector2(0f, d * 0.10f);
            _rouletteMask = mask.gameObject;
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            var rouletteGo = new GameObject("RouletteIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            rouletteGo.transform.SetParent(mask.transform, false);
            _rouletteImage = rouletteGo.GetComponent<RawImage>();
            _rouletteImage.texture = _rouletteIcon;
            _rouletteImage.raycastTarget = false;
            _rouletteImage.rectTransform.sizeDelta = Vector2.one * d * 0.40f;

            _cardImage = CreateImage("CardBackIcon", _visual, _cardBackIcon, Color.white, d * 0.36f);
            _cardImage.preserveAspect = true;
            _cardImage.rectTransform.anchoredPosition = new Vector2(0f, d * 0.10f);
            _cardImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -10f);

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(_visual, false);
            _label = labelGo.GetComponent<TextMeshProUGUI>();
            if (_font != null)
            {
                _label.font = _font;
            }
            _label.fontSize = _labelSize;
            _label.fontStyle = FontStyles.Bold;
            _label.color = _labelColor;
            _label.alignment = TextAlignmentOptions.Center;
            _label.textWrappingMode = TextWrappingModes.NoWrap;
            _label.raycastTarget = false;
            _label.outlineWidth = 0.22f;
            _label.outlineColor = _labelOutlineColor;
            var lr = _label.rectTransform;
            lr.sizeDelta = new Vector2(d * 0.62f, d * 0.24f);
            lr.anchoredPosition = new Vector2(0f, -d * 0.20f);

            // 既存のボタンは、チップ全体を覆う透明なタップ領域として最前面に置く。
            foreach (var button in new[] { _spinButton, _drawButton })
            {
                if (button == null)
                {
                    continue;
                }
                button.transform.SetAsLastSibling();
                var relay = button.GetComponent<TurnActionChipPressRelay>();
                if (relay == null)
                {
                    relay = button.gameObject.AddComponent<TurnActionChipPressRelay>();
                }
                relay.SetChip(this);
            }

            ApplyFace(Face.Spin);
        }

        private static Image CreateImage(string name, Transform parent, Sprite sprite, Color color, float size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            var r = image.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = Vector2.one * size;
            return image;
        }
    }
}
