using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LuckyTrash.UI
{
    /// <summary>
    /// 画面右上の山札の残り枚数表示。左上のネームプレート（トランプ風）と同じアイボリーのカード型の枠に、
    /// カードの裏面を数枚重ねたアイコンと、スロットのドラムのように縦に回る残り枚数の数字を並べる。
    /// 1枚減るときは変わる桁だけが1コマ回り、アイコンの一番上のカードが右上へ抜ける。
    /// 山札が捨て札から作り直されたときは「SHUFFLE!」のスタンプ、カードのシャッフル、数字が勢いよく回って
    /// 増える動きを見せる。どの動きもこのビューの中だけで再生し、ターン進行は待たせない。
    /// 見た目は新しい画像素材を使わず、Image・TextMeshPro・Outline・RectMask2D で組み立てる。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class DeckCounterView : MonoBehaviour
    {
        [Header("素材")]
        [Tooltip("角丸の板（9スライス）。Assets/Textures/RoundedPanel.png。ネームプレートと同じもの。")]
        [SerializeField] private Sprite _roundedSprite;
        [Tooltip("カードの裏面（Free Playing Cards Pack の BackColor_Blue）。盤面の山札と同じ柄。")]
        [SerializeField] private Sprite _cardBackSprite;
        [Tooltip("「山札」「枚」など日本語の文字のフォント。")]
        [SerializeField] private TMP_FontAsset _textFont;
        [Tooltip("数字・「SHUFFLE!」のフォント（LiberationSans SDF - Fallback）。")]
        [SerializeField] private TMP_FontAsset _numberFont;

        [Header("大きさ・余白（Canvasの参照解像度基準の単位）")]
        [Tooltip("枠の大きさ。高さはネームプレートにそろえる。")]
        [SerializeField] private Vector2 _size = new Vector2(220f, 78f);
        [Tooltip("画面の右端・上端からの余白（上端はネームプレートと同じ値にそろえる）。")]
        [SerializeField] private Vector2 _screenMargin = new Vector2(24f, 20f);
        [Tooltip("アイコンに重ねるカードの枚数。")]
        [Range(1, 4)] [SerializeField] private int _iconCardCount = 3;
        [Tooltip("数字の大きさ。")]
        [SerializeField] private float _numberSize = 46f;

        [Header("色（ネームプレートのトランプ風と同じ既定値）")]
        [SerializeField] private Color _cardColor = new Color(1f, 0.98f, 0.92f);
        [SerializeField] private Color _edgeColor = new Color(0.55f, 0.52f, 0.46f, 0.9f);
        [SerializeField] private Color _labelColor = new Color(0.38f, 0.36f, 0.33f);
        [SerializeField] private Color _numberColor = new Color(0.10f, 0.10f, 0.12f);
        [Tooltip("残りが少ないときの数字の色。")]
        [SerializeField] private Color _lowNumberColor = new Color(0.80f, 0.10f, 0.12f);

        [Header("数字の回転")]
        [Tooltip("1枚減るときに、変わる桁が1コマ回る時間（秒）。")]
        [SerializeField] private float _rollSeconds = 0.25f;
        [Tooltip("作り直しで増えるときに、ドラムが回る時間（秒）。")]
        [SerializeField] private float _reshuffleRollSeconds = 0.5f;
        [Tooltip("作り直しで増えるときに、変わる桁に追加で回す全周の回数（勢いを出す）。")]
        [SerializeField] private int _reshuffleExtraTurns = 1;

        [Header("カードが抜ける動き")]
        [Tooltip("1枚減るときに、アイコンの一番上のカードが右上へ抜けて消える時間（秒）。")]
        [SerializeField] private float _cardFlySeconds = 0.25f;
        [SerializeField] private Vector2 _cardFlyOffset = new Vector2(34f, 28f);

        [Header("残りが少ないときの強調")]
        [Tooltip("残りがこの枚数以下になったら数字を赤くする。")]
        [SerializeField] private int _lowThreshold = 10;
        [Tooltip("残りが少ないとき、数字をゆっくり脈打たせる。")]
        [SerializeField] private bool _pulseWhenLow = true;
        [SerializeField] private float _pulsePeriod = 1.4f;
        [SerializeField] private float _pulseScale = 0.06f;

        [Header("作り直し（SHUFFLE!）")]
        [Tooltip("「SHUFFLE!」を表示している時間（秒）。")]
        [SerializeField] private float _shuffleStampSeconds = 0.6f;
        [Tooltip("「SHUFFLE!」の文字の大きさ。")]
        [SerializeField] private float _shuffleStampSize = 34f;
        [Tooltip("スタンプを押した瞬間の拡大率（ここから元の大きさに収まる）。")]
        [SerializeField] private float _shuffleStampStartScale = 1.7f;
        [SerializeField] private Color _shuffleStampColor = new Color(1f, 0.80f, 0.20f);
        [SerializeField] private Color _shuffleStampOutlineColor = new Color(0.70f, 0.08f, 0.08f);
        [Tooltip("アイコンのカードをシャッフルするように左右へずらす時間（秒）と回数・幅。")]
        [SerializeField] private float _shuffleShakeSeconds = 0.45f;
        [SerializeField] private int _shuffleShakeCount = 3;
        [SerializeField] private float _shuffleShakeWidth = 9f;

        private const int DigitCount = 2;

        private RectTransform[] _cards;
        private Vector2[] _cardRestPositions;
        private float[] _cardRestAngles;
        private RectTransform _digitsRoot;
        private RectTransform[] _strips;
        private TMP_Text[][] _digitLabels;
        private float _digitHeight;
        private TMP_Text _stamp;

        private readonly float[] _digitFrom = new float[DigitCount];
        private readonly float[] _digitTo = new float[DigitCount];
        private float _rollElapsed = -1f;
        private float _rollDuration;
        private int _count = -1;
        private Coroutine _cardRoutine;
        private Coroutine _stampRoutine;
        private float _pulseTime;

        /// <summary>現在表示している（または切り替え先の）残り枚数。</summary>
        public int Count => _count;

        private void Awake()
        {
            Build();
        }

        /// <summary>
        /// 残り枚数を設定する。最初の1回は回さずにそのまま表示し、以後は変わった桁だけをドラムのように回す。
        /// 減ったときは、アイコンの一番上のカードが抜ける動きも再生する。
        /// </summary>
        public void SetCount(int count)
        {
            count = Mathf.Clamp(count, 0, 99);
            if (_count < 0)
            {
                _count = count;
                for (int i = 0; i < DigitCount; i++)
                {
                    _digitFrom[i] = _digitTo[i] = DeckCountDrum.GetDigit(count, DigitCount - 1 - i);
                }
                _rollElapsed = -1f;
                ApplyDigits(1f);
                ApplyNumberColor();
                return;
            }

            if (count == _count)
            {
                return;
            }

            bool decreased = count < _count;
            StartRoll(count, _rollSeconds, 0);
            if (decreased)
            {
                PlayCardFly();
            }
        }

        /// <summary>
        /// 山札が捨て札から作り直されたときの演出（SHUFFLE! のスタンプ、カードのシャッフル、数字が回って増える）。
        /// newCount は作り直したあとの残り枚数（作り直した直後に1枚引かれた後の枚数）。
        /// </summary>
        public void PlayReshuffle(int newCount)
        {
            newCount = Mathf.Clamp(newCount, 0, 99);
            if (_count < 0)
            {
                SetCount(newCount);
                return;
            }

            if (_stampRoutine != null)
            {
                StopCoroutine(_stampRoutine);
            }
            _stampRoutine = StartCoroutine(ShowStamp());

            if (_cardRoutine != null)
            {
                StopCoroutine(_cardRoutine);
                ResetCards();
            }
            _cardRoutine = StartCoroutine(ShakeCards());

            StartRoll(newCount, _reshuffleRollSeconds, _reshuffleExtraTurns);
        }

        private void Update()
        {
            if (_rollElapsed >= 0f)
            {
                _rollElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(_rollElapsed / Mathf.Max(0.01f, _rollDuration));
                // 回り始めは速く、止まる直前に減速する（ドラムが止まる感じ）。
                ApplyDigits(1f - (1f - t) * (1f - t) * (1f - t));
                if (t >= 1f)
                {
                    _rollElapsed = -1f;
                }
            }

            bool low = _count >= 0 && _count <= _lowThreshold;
            if (low && _pulseWhenLow)
            {
                _pulseTime += Time.deltaTime;
                float wave = 0.5f - 0.5f * Mathf.Cos(_pulseTime * Mathf.PI * 2f / Mathf.Max(0.1f, _pulsePeriod));
                _digitsRoot.localScale = Vector3.one * (1f + _pulseScale * wave);
            }
            else
            {
                _pulseTime = 0f;
                _digitsRoot.localScale = Vector3.one;
            }
        }

        private void StartRoll(int target, float duration, int extraTurns)
        {
            int[] steps = DeckCountDrum.GetSteps(_count, target, DigitCount, extraTurns);
            for (int i = 0; i < DigitCount; i++)
            {
                // 回転中に次の切り替えが来ても、いま表示している位置から回し直す。
                float current = CurrentDigitPosition(i);
                float fromDigit = DeckCountDrum.GetDigit(_count, DigitCount - 1 - i);
                _digitFrom[i] = steps[i] == 0 ? fromDigit : current;
                _digitTo[i] = fromDigit + steps[i];
            }
            _count = target;
            _rollDuration = duration;
            _rollElapsed = 0f;
            ApplyNumberColor();
        }

        private float CurrentDigitPosition(int digit)
        {
            if (_rollElapsed < 0f)
            {
                return _digitTo[digit];
            }
            float t = Mathf.Clamp01(_rollElapsed / Mathf.Max(0.01f, _rollDuration));
            return Mathf.Lerp(_digitFrom[digit], _digitTo[digit], 1f - (1f - t) * (1f - t) * (1f - t));
        }

        /// <summary>各桁の帯を、進み具合 eased（0〜1）に応じた位置へスクロールする。</summary>
        private void ApplyDigits(float eased)
        {
            for (int i = 0; i < DigitCount; i++)
            {
                float position = Mathf.Lerp(_digitFrom[i], _digitTo[i], eased);
                // 帯は 0〜9 と、つなぎ目用にもう一度 0 を並べた11コマ。10コマで一周するよう折り返す。
                float wrapped = Mathf.Repeat(position, 10f);
                _strips[i].anchoredPosition = new Vector2(0f, wrapped * _digitHeight);
            }
        }

        private void ApplyNumberColor()
        {
            Color color = _count >= 0 && _count <= _lowThreshold ? _lowNumberColor : _numberColor;
            foreach (var labels in _digitLabels)
            {
                foreach (var label in labels)
                {
                    label.color = color;
                }
            }
        }

        private void PlayCardFly()
        {
            if (_cardRoutine != null)
            {
                StopCoroutine(_cardRoutine);
                ResetCards();
            }
            _cardRoutine = StartCoroutine(FlyTopCard());
        }

        private IEnumerator FlyTopCard()
        {
            var top = _cards[_cards.Length - 1];
            var image = top.GetComponent<Image>();
            int index = _cards.Length - 1;
            float duration = Mathf.Max(0.01f, _cardFlySeconds);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - (1f - t) * (1f - t);
                top.anchoredPosition = _cardRestPositions[index] + _cardFlyOffset * eased;
                top.localRotation = Quaternion.Euler(0f, 0f, _cardRestAngles[index] - 18f * eased);
                image.color = new Color(1f, 1f, 1f, 1f - t);
                yield return null;
            }
            // 抜けたあとは、元の枚数の見た目に戻す。
            ResetCards();
            _cardRoutine = null;
        }

        private IEnumerator ShakeCards()
        {
            float duration = Mathf.Max(0.01f, _shuffleShakeSeconds);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float wave = Mathf.Sin(t * Mathf.PI * 2f * Mathf.Max(1, _shuffleShakeCount)) * (1f - t);
                for (int i = 0; i < _cards.Length; i++)
                {
                    float direction = i % 2 == 0 ? 1f : -1f;
                    _cards[i].anchoredPosition = _cardRestPositions[i] + new Vector2(direction * _shuffleShakeWidth * wave, 0f);
                }
                yield return null;
            }
            ResetCards();
            _cardRoutine = null;
        }

        private IEnumerator ShowStamp()
        {
            _stamp.gameObject.SetActive(true);
            float duration = Mathf.Max(0.05f, _shuffleStampSeconds);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // 最初の2割で大きい状態から元の大きさに押し付け、最後の3割でフェードアウトする。
                float press = Mathf.Clamp01(t / 0.2f);
                float scale = Mathf.Lerp(_shuffleStampStartScale, 1f, 1f - (1f - press) * (1f - press));
                float alpha = t < 0.7f ? Mathf.Clamp01(t / 0.08f) : Mathf.Clamp01((1f - t) / 0.3f);
                _stamp.rectTransform.localScale = Vector3.one * scale;
                _stamp.alpha = alpha;
                yield return null;
            }
            _stamp.gameObject.SetActive(false);
            _stampRoutine = null;
        }

        private void ResetCards()
        {
            for (int i = 0; i < _cards.Length; i++)
            {
                _cards[i].anchoredPosition = _cardRestPositions[i];
                _cards[i].localRotation = Quaternion.Euler(0f, 0f, _cardRestAngles[i]);
                _cards[i].GetComponent<Image>().color = Color.white;
            }
        }

        // ------------------------------------------------------------------
        // 見た目の組み立て
        // ------------------------------------------------------------------

        private void Build()
        {
            var rect = (RectTransform)transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = _size;
            rect.anchoredPosition = new Vector2(-_screenMargin.x, -_screenMargin.y);

            float w = _size.x, h = _size.y;

            // ネームプレート（トランプ風）と同じ質感: 影、アイボリーの角丸、細い縁取り。
            var shadow = CreateImage("Shadow", transform, _roundedSprite, new Color(0f, 0f, 0f, 0.28f));
            Stretch(shadow.rectTransform);
            shadow.rectTransform.anchoredPosition = new Vector2(3f, -3f);
            var body = CreateImage("Body", transform, _roundedSprite, _cardColor);
            Stretch(body.rectTransform);
            var edge = body.gameObject.AddComponent<Outline>();
            edge.effectColor = _edgeColor;
            edge.effectDistance = new Vector2(1f, -1f);

            // 左側: カードの裏面を少しずつずらして重ねたアイコン（一番上が最後）。
            int n = Mathf.Max(1, _iconCardCount);
            _cards = new RectTransform[n];
            _cardRestPositions = new Vector2[n];
            _cardRestAngles = new float[n];
            float iconCenterX = -w / 2f + 40f;
            for (int i = 0; i < n; i++)
            {
                var card = CreateImage($"CardBack_{i}", transform, _cardBackSprite, Color.white);
                card.preserveAspect = true;
                var r = card.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                r.sizeDelta = new Vector2(h * 0.78f, h * 0.78f);
                _cardRestPositions[i] = new Vector2(iconCenterX - 6f + i * 5f, -2f + i * 2.5f);
                _cardRestAngles[i] = -8f + i * 6f;
                _cards[i] = r;
            }
            ResetCards();

            // 右側: 小さく「山札」、その下に大きく数字（ドラム）、数字の横に小さく「枚」。
            float textLeft = -w / 2f + 82f;
            var title = CreateText("Title", transform, _textFont, "山札", 16f, _labelColor, TextAlignmentOptions.Left);
            Place(title.rectTransform, new Vector2(textLeft + 40f, h / 2f - 14f), new Vector2(80f, 20f));

            _digitHeight = _numberSize * 1.05f;
            float digitWidth = _numberSize * 0.62f;
            _digitsRoot = new GameObject("Digits", typeof(RectTransform)).GetComponent<RectTransform>();
            _digitsRoot.SetParent(transform, false);
            Place(_digitsRoot, new Vector2(textLeft + digitWidth * DigitCount / 2f + 4f, -8f), new Vector2(digitWidth * DigitCount, _digitHeight));

            _strips = new RectTransform[DigitCount];
            _digitLabels = new TMP_Text[DigitCount][];
            for (int d = 0; d < DigitCount; d++)
            {
                var window = new GameObject($"DigitWindow_{d}", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
                window.SetParent(_digitsRoot, false);
                window.anchorMin = window.anchorMax = new Vector2(0f, 0.5f);
                window.pivot = new Vector2(0f, 0.5f);
                window.sizeDelta = new Vector2(digitWidth, _digitHeight);
                window.anchoredPosition = new Vector2(d * digitWidth, 0f);

                var strip = new GameObject("Strip", typeof(RectTransform)).GetComponent<RectTransform>();
                strip.SetParent(window, false);
                strip.anchorMin = strip.anchorMax = new Vector2(0.5f, 1f);
                strip.pivot = new Vector2(0.5f, 1f);
                strip.sizeDelta = new Vector2(digitWidth, _digitHeight * 11f);
                _strips[d] = strip;

                // 0〜9 と、つなぎ目用の 0。最上位桁の 0 は空白にする（「09」ではなく「9」と見せる）。
                bool leading = d == 0;
                _digitLabels[d] = new TMP_Text[11];
                for (int k = 0; k < 11; k++)
                {
                    int value = k % 10;
                    string text = leading && value == 0 ? string.Empty : value.ToString();
                    var label = CreateText($"D{k}", strip, _numberFont, text, _numberSize, _numberColor, TextAlignmentOptions.Center);
                    label.fontStyle = FontStyles.Bold;
                    var lr = label.rectTransform;
                    lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 1f);
                    lr.pivot = new Vector2(0.5f, 1f);
                    lr.sizeDelta = new Vector2(digitWidth, _digitHeight);
                    lr.anchoredPosition = new Vector2(0f, -k * _digitHeight);
                    _digitLabels[d][k] = label;
                }
            }

            var unit = CreateText("Unit", transform, _textFont, "枚", 18f, _labelColor, TextAlignmentOptions.Left);
            Place(unit.rectTransform, new Vector2(textLeft + digitWidth * DigitCount + 20f, -18f), new Vector2(24f, 24f));

            // 枠のすぐ下に出す「SHUFFLE!」のスタンプ（普段は非表示）。
            _stamp = CreateText("ShuffleStamp", transform, _numberFont, "SHUFFLE!", _shuffleStampSize, _shuffleStampColor, TextAlignmentOptions.Center);
            _stamp.fontStyle = FontStyles.Bold | FontStyles.Italic;
            _stamp.outlineWidth = 0.25f;
            _stamp.outlineColor = _shuffleStampOutlineColor;
            Place(_stamp.rectTransform, new Vector2(0f, -h / 2f - _shuffleStampSize * 0.7f), new Vector2(w, _shuffleStampSize * 1.3f));
            _stamp.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -6f);
            _stamp.gameObject.SetActive(false);
        }

        private static Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            if (sprite != null && sprite.border != Vector4.zero)
            {
                image.type = Image.Type.Sliced;
            }
            return image;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, TMP_FontAsset font, string text,
            float size, Color color, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (font != null)
            {
                tmp.font = font;
            }
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>枠の中心を原点として、position を中心に size の大きさで置く。</summary>
        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }
    }
}
