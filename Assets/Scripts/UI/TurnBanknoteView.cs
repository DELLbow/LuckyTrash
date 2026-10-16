using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LuckyTrash.UI
{
    /// <summary>
    /// 手番が変わったときに、架空の紙幣（LUCKY TRASH BANK）が画面の右外から入って中央で少し止まり、
    /// 左外へ抜けていく演出。ターン進行とは並行して再生するだけで、呼び出し元を待たせない。
    /// 紙幣の見た目は新しい画像素材を使わず、Image・TextMeshPro・Outline で組み立てる。
    /// どのグラフィックもタップを受け取らないため、紙幣が流れている間もボタンは操作できる。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class TurnBanknoteView : MonoBehaviour
    {
        // 1P=♠, 2P=♥, 3P=♦, 4P=♣（ネームプレートと同じ割り当て）。
        private static readonly string[] Suits = { "♠", "♥", "♦", "♣" };

        [Header("素材")]
        [Tooltip("角丸の板（9スライス）。Assets/Textures/RoundedPanel.png。")]
        [SerializeField] private Sprite _roundedSprite;
        [Tooltip("円（UI/Skin/Knob）。肖像の位置に見立てた楕円の枠に使う。")]
        [SerializeField] private Sprite _circleSprite;
        [Tooltip("「〇〇の番です」など日本語の文字のフォント。")]
        [SerializeField] private TMP_FontAsset _textFont;
        [Tooltip("♠♥♦♣・英字（LUCKY TRASH BANK、1P など）のフォント。ネームプレートと同じ LiberationSans SDF - Fallback。")]
        [SerializeField] private TMP_FontAsset _symbolFont;

        [Header("大きさ・位置（Canvasの参照解像度基準の単位）")]
        [Tooltip("自分の番の紙幣の大きさ。")]
        [SerializeField] private Vector2 _humanSize = new Vector2(560f, 230f);
        [Tooltip("CPUの番の紙幣の大きさ（一回り小さく）。")]
        [SerializeField] private Vector2 _cpuSize = new Vector2(440f, 180f);
        [Tooltip("紙幣の上端を、画面の上端からどれだけ下に置くか（ネームプレートと山札残り枚数表示より下）。")]
        [SerializeField] private float _topOffset = 240f;

        [Header("動き")]
        [Tooltip("右外から中央まで入ってくる時間（秒）。")]
        [SerializeField] private float _enterSeconds = 0.25f;
        [Tooltip("左外へ抜けていく時間（秒）。")]
        [SerializeField] private float _exitSeconds = 0.25f;
        [Tooltip("自分の番で、中央で止まっている時間（秒）。")]
        [SerializeField] private float _humanHoldSeconds = 0.8f;
        [Tooltip("CPUの番で、中央で止まっている時間（秒）。テンポ優先で短く。")]
        [SerializeField] private float _cpuHoldSeconds = 0.3f;
        [Tooltip("入ってくるときの傾き（度）。中央で止まるときに水平へ戻る。")]
        [SerializeField] private float _enterTiltDegrees = 6f;
        [Tooltip("出ていくときの傾き（度）。")]
        [SerializeField] private float _exitTiltDegrees = -5f;
        [Tooltip("止まるときに勢いで行き過ぎる量。")]
        [SerializeField] private float _overshoot = 22f;

        [Header("色")]
        [SerializeField] private Color _paperColor = new Color(0.86f, 0.93f, 0.83f);
        [Tooltip("模様・文字のインクの色（紙幣の上でも読みやすい濃い緑）。")]
        [SerializeField] private Color _inkColor = new Color(0.10f, 0.27f, 0.18f);
        [Tooltip("自分の番の紙幣の縁取りの金色。")]
        [SerializeField] private Color _goldColor = new Color(0.83f, 0.66f, 0.18f);
        [SerializeField] private Color _suitRedColor = new Color(0.80f, 0.10f, 0.12f);
        [SerializeField] private Color _suitBlackColor = new Color(0.10f, 0.10f, 0.12f);

        private RectTransform _note;
        private Coroutine _playing;

        /// <summary>
        /// 手番の紙幣を流す。進行中の紙幣があれば打ち切って、新しい紙幣を右外から流し直す。
        /// </summary>
        /// <param name="orderNumber">手番の順番（1始まり、「1P」の数字。スートもこれで決まる）。</param>
        /// <param name="message">紙幣に書く文字（「あなたの番です」「CPU 2の番です」など）。</param>
        /// <param name="isHuman">自分の番か（大きく、縁取りを金色にし、長めに止める）。</param>
        public void Play(int orderNumber, string message, bool isHuman)
        {
            if (_playing != null)
            {
                StopCoroutine(_playing);
            }
            if (_note != null)
            {
                Destroy(_note.gameObject);
            }

            _note = BuildNote(orderNumber, message, isHuman);
            _playing = StartCoroutine(Animate(_note, isHuman ? _humanHoldSeconds : _cpuHoldSeconds));
        }

        private IEnumerator Animate(RectTransform note, float holdSeconds)
        {
            float canvasWidth = ((RectTransform)transform).rect.width;
            float offscreen = canvasWidth / 2f + note.sizeDelta.x / 2f + 40f;

            // 入る: 右外から中央を少し行き過ぎて戻る。傾きは水平へ戻る。
            float enter = Mathf.Max(0.01f, _enterSeconds);
            float elapsed = 0f;
            while (elapsed < enter)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / enter);
                float x;
                if (t < 0.75f)
                {
                    float u = t / 0.75f;
                    x = Mathf.Lerp(offscreen, -_overshoot, 1f - (1f - u) * (1f - u));
                }
                else
                {
                    float u = (t - 0.75f) / 0.25f;
                    x = Mathf.Lerp(-_overshoot, 0f, Mathf.SmoothStep(0f, 1f, u));
                }
                Place(note, x, Mathf.Lerp(_enterTiltDegrees, 0f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.9f))));
                yield return null;
            }
            Place(note, 0f, 0f);

            if (holdSeconds > 0f)
            {
                yield return new WaitForSeconds(holdSeconds);
            }

            // 出る: 左へ加速しながら、また少し傾く。
            float exit = Mathf.Max(0.01f, _exitSeconds);
            elapsed = 0f;
            while (elapsed < exit)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / exit);
                Place(note, Mathf.Lerp(0f, -offscreen, t * t), Mathf.Lerp(0f, _exitTiltDegrees, t));
                yield return null;
            }

            Destroy(note.gameObject);
            if (_note == note)
            {
                _note = null;
            }
            _playing = null;
        }

        private void Place(RectTransform note, float x, float tilt)
        {
            note.anchoredPosition = new Vector2(x, -_topOffset - note.sizeDelta.y / 2f);
            note.localRotation = Quaternion.Euler(0f, 0f, tilt);
        }

        // ------------------------------------------------------------------
        // 紙幣の組み立て
        // ------------------------------------------------------------------

        private RectTransform BuildNote(int orderNumber, string message, bool isHuman)
        {
            Vector2 size = isHuman ? _humanSize : _cpuSize;
            float s = size.y / 230f; // 文字や線の太さを紙幣の大きさに比例させるための倍率
            int suitIndex = (Mathf.Max(1, orderNumber) - 1) % Suits.Length;
            Color suitColor = suitIndex == 1 || suitIndex == 2 ? _suitRedColor : _suitBlackColor;
            Color frameColor = isHuman ? _goldColor : _inkColor;
            string orderLabel = $"{orderNumber}P";

            var note = new GameObject("Banknote", typeof(RectTransform)).GetComponent<RectTransform>();
            note.SetParent(transform, false);
            note.anchorMin = note.anchorMax = new Vector2(0.5f, 1f);
            note.pivot = new Vector2(0.5f, 0.5f);
            note.sizeDelta = size;
            Place(note, ((RectTransform)transform).rect.width, _enterTiltDegrees);

            // 影と紙。
            var shadow = CreateImage("Shadow", note, _roundedSprite, new Color(0f, 0f, 0f, 0.30f));
            Stretch(shadow.rectTransform, 0f);
            shadow.rectTransform.anchoredPosition = new Vector2(6f * s, -8f * s);
            shadow.pixelsPerUnitMultiplier = 3f;

            var paper = CreateImage("Paper", note, _roundedSprite, _paperColor);
            Stretch(paper.rectTransform, 0f);
            paper.pixelsPerUnitMultiplier = 3f;
            var paperEdge = paper.gameObject.AddComponent<Outline>();
            paperEdge.effectColor = frameColor;
            paperEdge.effectDistance = Vector2.one * (isHuman ? 3f : 1.5f) * s * new Vector2(1f, -1f);

            // 地の細かい横線（紙幣らしい細かさ）。
            for (float y = 18f * s; y < size.y - 18f * s; y += 6f * s)
            {
                var line = CreateImage("Hatch", note, null, new Color(_inkColor.r, _inkColor.g, _inkColor.b, 0.06f));
                var r = line.rectTransform;
                r.anchorMin = new Vector2(0f, 0f);
                r.anchorMax = new Vector2(1f, 0f);
                r.offsetMin = new Vector2(20f * s, y);
                r.offsetMax = new Vector2(-20f * s, y + 1f);
            }

            // 縁の模様: 細い線の枠を三重に重ねる（自分の番は一番外側を金色に）。
            FrameLines(note, 7f * s, 2.5f * s, frameColor);
            FrameLines(note, 12f * s, 1f * s, _inkColor);
            FrameLines(note, 15f * s, 1f * s, new Color(_inkColor.r, _inkColor.g, _inkColor.b, 0.6f));

            // 四隅: 菱形の飾りと、額面のように手番の順番。
            Vector2[] corners = { new Vector2(-1f, 1f), new Vector2(1f, 1f), new Vector2(-1f, -1f), new Vector2(1f, -1f) };
            foreach (var c in corners)
            {
                Vector2 pos = new Vector2(c.x * (size.x / 2f - 40f * s), c.y * (size.y / 2f - 36f * s));

                var medal = CreateImage("CornerMedal", note, _circleSprite, _paperColor);
                Center(medal.rectTransform, pos, Vector2.one * 46f * s);
                var medalEdge = medal.gameObject.AddComponent<Outline>();
                medalEdge.effectColor = frameColor;
                medalEdge.effectDistance = new Vector2(1.5f, -1.5f) * s;

                var label = CreateText("CornerOrder", note, _symbolFont, orderLabel, 20f * s, _inkColor);
                label.fontStyle = FontStyles.Bold;
                Center(label.rectTransform, pos, Vector2.one * 46f * s);

                var gem = CreateImage("CornerOrnament", note, null, frameColor);
                Center(gem.rectTransform, new Vector2(pos.x + c.x * -34f * s, pos.y), Vector2.one * 7f * s);
                gem.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            }

            // 上部中央の銀行名。
            var bank = CreateText("BankName", note, _symbolFont, "LUCKY TRASH BANK", 16f * s, _inkColor);
            bank.fontStyle = FontStyles.Bold;
            bank.characterSpacing = 12f;
            Center(bank.rectTransform, new Vector2(0f, size.y / 2f - 30f * s), new Vector2(size.x * 0.6f, 24f * s));

            // 肖像の位置に見立てた楕円の枠と、その中のスート。
            Vector2 ovalPos = new Vector2(-size.x * 0.22f, -6f * s);
            var ovalOuter = CreateImage("OvalFrame", note, _circleSprite, frameColor);
            Center(ovalOuter.rectTransform, ovalPos, new Vector2(118f, 150f) * s);
            var ovalRing = CreateImage("OvalRing", note, _circleSprite, _paperColor);
            Center(ovalRing.rectTransform, ovalPos, new Vector2(110f, 142f) * s);
            var ovalInner = CreateImage("OvalInner", note, _circleSprite, new Color(_inkColor.r, _inkColor.g, _inkColor.b, 0.55f));
            Center(ovalInner.rectTransform, ovalPos, new Vector2(102f, 134f) * s);
            var ovalField = CreateImage("OvalField", note, _circleSprite, Color.Lerp(_paperColor, Color.white, 0.45f));
            Center(ovalField.rectTransform, ovalPos, new Vector2(98f, 130f) * s);

            var suit = CreateText("Suit", note, _symbolFont, Suits[suitIndex], 86f * s, suitColor);
            Center(suit.rectTransform, ovalPos, new Vector2(98f, 130f) * s);

            // 楕円の横に「〇〇の番です」。
            float textLeft = ovalPos.x + 70f * s;
            float textRight = size.x / 2f - 30f * s;
            var text = CreateText("Message", note, _textFont, message, 40f * s, _inkColor);
            text.fontStyle = FontStyles.Bold;
            text.enableAutoSizing = true;
            text.fontSizeMin = 18f * s;
            text.fontSizeMax = 40f * s;
            text.overflowMode = TextOverflowModes.Ellipsis;
            Center(text.rectTransform, new Vector2((textLeft + textRight) / 2f, -4f * s), new Vector2(textRight - textLeft, 64f * s));

            // 文字の下の飾り線。
            var rule = CreateImage("Rule", note, null, new Color(frameColor.r, frameColor.g, frameColor.b, 0.8f));
            Center(rule.rectTransform, new Vector2((textLeft + textRight) / 2f, -40f * s), new Vector2((textRight - textLeft) * 0.8f, 1.5f * s));

            return note;
        }

        /// <summary>紙幣の外周から inset 内側に、太さ width の細い線で長方形の枠を描く。</summary>
        private void FrameLines(RectTransform parent, float inset, float width, Color color)
        {
            AddLine(parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(inset, -inset - width), new Vector2(-inset, -inset), color);
            AddLine(parent, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(inset, inset), new Vector2(-inset, inset + width), color);
            AddLine(parent, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(inset, inset), new Vector2(inset + width, -inset), color);
            AddLine(parent, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-inset - width, inset), new Vector2(-inset, -inset), color);
        }

        private void AddLine(RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            var line = CreateImage("FrameLine", parent, null, color);
            var r = line.rectTransform;
            r.anchorMin = anchorMin;
            r.anchorMax = anchorMax;
            r.offsetMin = offsetMin;
            r.offsetMax = offsetMax;
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

        private static TextMeshProUGUI CreateText(string name, Transform parent, TMP_FontAsset font, string text, float size, Color color)
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
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.one * inset;
            rect.offsetMax = -Vector2.one * inset;
        }

        private static void Center(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }
    }
}
