using System.Collections;
using System.Collections.Generic;
using LuckyTrash.UI.Scoring;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LuckyTrash.UI
{
    /// <summary>
    /// ターンの結果を、銀行の伝票（横長の小さな紙）にスタンプを押したように見せる演出。
    /// まず画面中央に伝票（ネームプレート・紙幣と同じアイボリーの紙、上部に小さく「LUCKY TRASH BANK」と細い罫線）が
    /// すっと出てきて、その直後にスタンプが押される。
    /// トラッシュのときは赤いインクの二重線の枠に「トラッシュ！」「×N枚」を、ランダムに傾けて勢いよく押す。
    /// ノートラッシュのときは墨色（紺寄りの黒）の「ノートラッシュ…」を弱く押し、伝票ごとずるっと下へずり落ちて消える。
    /// 新しい画像素材は使わず、紙は角丸の板、枠は細い Image の線分（インクのむらとして1本ごとに濃さを少し変える）、
    /// 文字は TextMeshPro（太字＋同じ色の縁取りで太く、頂点グラデーションで弱いかすれ）で組み立てる。
    /// どのグラフィックもタップを受け取らない。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class BankStampView : MonoBehaviour
    {
        [Header("素材")]
        [Tooltip("「トラッシュ！」などの日本語の文字のフォント。")]
        [SerializeField] private TMP_FontAsset _textFont;
        [Tooltip("「LUCKY TRASH BANK」の英字のフォント（紙幣・ネームプレートと同じ LiberationSans SDF - Fallback）。")]
        [SerializeField] private TMP_FontAsset _headerFont;
        [Tooltip("伝票の紙に使う角丸の板（9スライス）。Assets/Textures/RoundedPanel.png。")]
        [SerializeField] private Sprite _paperSprite;

        [Header("大きさ・位置（Canvasの参照解像度基準の単位）")]
        [Tooltip("自分の番の伝票の大きさ。")]
        [SerializeField] private Vector2 _humanSlipSize = new Vector2(560f, 280f);
        [Tooltip("CPUの番の伝票の大きさ。")]
        [SerializeField] private Vector2 _cpuSlipSize = new Vector2(500f, 250f);
        [Tooltip("自分の番のスタンプの大きさ（伝票の内側に収まる大きさにする）。")]
        [SerializeField] private Vector2 _humanSize = new Vector2(440f, 185f);
        [Tooltip("CPUの番のスタンプの大きさ。")]
        [SerializeField] private Vector2 _cpuSize = new Vector2(390f, 165f);
        [Tooltip("画面中央からのずれ。伝票は不透明なので、盤面（向かいの手札の列・捨て札の山・ルーレット）を隠さないよう、テーブルより上の空のあたりに出す。")]
        [SerializeField] private Vector2 _centerOffset = new Vector2(0f, 175f);
        [Tooltip("スタンプの傾きの最大値（度）。押すたびに ±この範囲でランダムに傾ける。")]
        [SerializeField] private float _maxTiltDegrees = 8f;
        [Tooltip("伝票の傾きの最大値（度）。少し斜めに置かれた見た目にする。")]
        [SerializeField] private float _slipTiltDegrees = 2f;

        [Header("時間（秒）")]
        [Tooltip("押されて現れるまでの時間（伝票が出てくる時間を含む）。")]
        [SerializeField] private float _appearSeconds = 0.15f;
        [Tooltip("伝票がすっと出てくる時間。スタンプはその直後に押される（押す瞬間は「押されて現れるまでの時間」の終わりで変わらない）。")]
        [SerializeField] private float _slipEnterSeconds = 0.1f;
        [Tooltip("消えるまでの時間。")]
        [SerializeField] private float _disappearSeconds = 0.15f;
        [Tooltip("自分の番で、押されたまま見せておく時間。")]
        [SerializeField] private float _humanHoldSeconds = 1.0f;
        [Tooltip("CPUの番で、押されたまま見せておく時間。")]
        [SerializeField] private float _cpuHoldSeconds = 0.6f;

        [Header("押し方")]
        [Tooltip("トラッシュのとき、押し始めの大きさ（倍率）。ここから一気に縮んで紙に押される。")]
        [SerializeField] private float _trashStartScale = 2.4f;
        [Tooltip("ノートラッシュのとき、押し始めの大きさ（倍率）。弱く押すので小さめ。")]
        [SerializeField] private float _noTrashStartScale = 1.3f;
        [Tooltip("押した瞬間の揺れの大きさ（度）。")]
        [SerializeField] private float _wobbleDegrees = 3f;
        [Tooltip("ノートラッシュの伝票とスタンプが、消えるまでにずり落ちる距離。")]
        [SerializeField] private float _noTrashSlideDistance = 90f;

        [Header("色・太さ")]
        [SerializeField] private Color _trashInkColor = new Color(0.78f, 0.06f, 0.08f, 1f);
        [Tooltip("ノートラッシュのインク（紺寄りの黒の墨色）。")]
        [SerializeField] private Color _noTrashInkColor = new Color(0.10f, 0.12f, 0.22f, 1f);
        [Tooltip("インクのむら：線分・文字の濃さを、この割合までランダムに薄くする。")]
        [Range(0f, 1f)]
        [SerializeField] private float _inkUnevenness = 0.1f;
        [Tooltip("スタンプの文字を太くする、同じ色の縁取りの太さ（TextMeshPro の Outline Width、0〜1）。")]
        [Range(0f, 1f)]
        [SerializeField] private float _textOutlineWidth = 0.2f;
        [Tooltip("伝票の紙の色（ネームプレートの札と同じアイボリー）。")]
        [SerializeField] private Color _paperColor = new Color(1f, 0.98f, 0.92f);
        [Tooltip("伝票の縁の色。")]
        [SerializeField] private Color _paperEdgeColor = new Color(0.55f, 0.52f, 0.46f, 0.9f);
        [Tooltip("「LUCKY TRASH BANK」の文字と罫線の色。")]
        [SerializeField] private Color _slipInkColor = new Color(0.30f, 0.32f, 0.36f);
        [Tooltip("伝票の影の濃さ。")]
        [Range(0f, 1f)]
        [SerializeField] private float _shadowAlpha = 0.3f;

        [Header("ボーナス")]
        [Tooltip("ミラクルトラッシュのときのスタンプのインク（金色）。")]
        [SerializeField] private Color _miracleInkColor = new Color(0.74f, 0.52f, 0.08f, 1f);
        [Tooltip("ボーナスの行（スタンプの下に添える「ビッグトラッシュ！ +20」など）の文字の色。")]
        [SerializeField] private Color _bonusLineColor = new Color(0.70f, 0.08f, 0.08f, 1f);
        [Tooltip("ボーナスの行1行あたりの高さ（伝票はこの分だけ伸び、全体が上にずれる）。")]
        [SerializeField] private float _bonusLineHeight = 40f;
        [Tooltip("スタンプとボーナスの行の間のすき間（スタンプが傾いても、枠の角が行にかからない広さ）。")]
        [SerializeField] private float _bonusLineGap = 26f;
        [Tooltip("ボーナスがあるとき、押したまま見せておく時間をどれだけ延ばすか（秒）。")]
        [SerializeField] private float _bonusExtraHoldSeconds = 0.3f;

        private RectTransform _root;
        private CanvasGroup _rootGroup;
        private RectTransform _slip;
        private CanvasGroup _slipGroup;
        private RectTransform _stamp;
        private CanvasGroup _stampGroup;
        private CanvasGroup _bonusLinesGroup;
        // 縁取りで太くした文字のマテリアル（インクの色ごとに1つ作って使い回す）。
        private readonly Dictionary<Color, Material> _textMaterials = new Dictionary<Color, Material>();

        /// <summary>
        /// 伝票を出してスタンプを押し、見せてから消えるまで待つコルーチン。discardedCount が 0 ならノートラッシュの押し方になる。
        /// bonuses があれば、スタンプの下にボーナス名と得点を1行ずつ添える（伝票はその分だけ伸び、見せる時間も少し延びる）。
        /// ミラクルトラッシュのときは、スタンプを金色のインクにして「ミラクルトラッシュ！ +100」と押す。
        /// </summary>
        public IEnumerator Play(int discardedCount, bool isHuman, IReadOnlyList<BonusAward> bonuses = null)
        {
            bool hasTrash = discardedCount > 0;
            BonusAward? miracle = null;
            var lines = new List<string>();
            if (bonuses != null)
            {
                foreach (var bonus in bonuses)
                {
                    if (bonus.Kind == BonusKind.MiracleTrash && miracle == null)
                    {
                        miracle = bonus;
                    }
                    else
                    {
                        lines.Add($"{bonus.Name}！ +{bonus.Points}");
                    }
                }
            }
            Color ink = miracle.HasValue ? _miracleInkColor : hasTrash ? _trashInkColor : _noTrashInkColor;
            Build(isHuman ? _humanSlipSize : _cpuSlipSize, isHuman ? _humanSize : _cpuSize, ink, hasTrash, discardedCount, miracle, lines);

            float tilt = Random.Range(-_maxTiltDegrees, _maxTiltDegrees);
            float hold = isHuman ? _humanHoldSeconds : _cpuHoldSeconds;
            if (miracle.HasValue || lines.Count > 0)
            {
                hold += _bonusExtraHoldSeconds;
            }
            float startScale = hasTrash ? _trashStartScale : _noTrashStartScale;

            // 伝票がすっと出てきて、その直後にスタンプが大きく浮いた状態から一気に縮んで紙に当たる。
            float appear = Mathf.Max(0.01f, _appearSeconds);
            float slipEnter = Mathf.Clamp(_slipEnterSeconds, 0f, appear);
            Vector2 slipRest = _slip.anchoredPosition;
            float elapsed = 0f;
            while (elapsed < appear)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / appear);
                float slipT = slipEnter > 0f ? Mathf.Clamp01(elapsed / slipEnter) : 1f;
                float slipEased = 1f - (1f - slipT) * (1f - slipT);
                _slip.anchoredPosition = slipRest + Vector2.down * 30f * (1f - slipEased);
                _slipGroup.alpha = slipEased;

                float eased = t * t;
                float scale = Mathf.Lerp(startScale, hasTrash ? 0.94f : 0.98f, eased);
                _stamp.localScale = Vector3.one * scale;
                _stamp.localRotation = Quaternion.Euler(0f, 0f, tilt + (1f - t) * tilt * 0.6f);
                float stampT = appear > slipEnter ? Mathf.Clamp01((elapsed - slipEnter) / (appear - slipEnter)) : (t >= 1f ? 1f : 0f);
                _stampGroup.alpha = stampT;
                if (_bonusLinesGroup != null)
                {
                    _bonusLinesGroup.alpha = stampT;
                }
                yield return null;
            }
            _slip.anchoredPosition = slipRest;
            _slipGroup.alpha = 1f;
            _stampGroup.alpha = 1f;
            if (_bonusLinesGroup != null)
            {
                _bonusLinesGroup.alpha = 1f;
            }

            // 押したまま見せる：最初の一瞬だけ跳ね返りと揺れ。ノートラッシュはこの間に伝票ごとゆっくりずり落ちる。
            Vector2 restPosition = _root.anchoredPosition;
            float settle = Mathf.Min(0.18f, hold);
            elapsed = 0f;
            while (elapsed < hold)
            {
                elapsed += Time.deltaTime;
                float s = settle > 0f ? Mathf.Clamp01(elapsed / settle) : 1f;
                float rebound = Mathf.Lerp(hasTrash ? 0.94f : 0.98f, 1f, 1f - (1f - s) * (1f - s));
                float wobble = Mathf.Sin(s * Mathf.PI * 3f) * (1f - s) * (hasTrash ? _wobbleDegrees : _wobbleDegrees * 0.4f);
                _stamp.localScale = Vector3.one * rebound;
                float slump = 0f;
                if (!hasTrash)
                {
                    float h = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, hold));
                    slump = h * h;
                }
                _stamp.localRotation = Quaternion.Euler(0f, 0f, tilt + wobble - slump * 4f);
                _root.anchoredPosition = restPosition + Vector2.down * slump * _noTrashSlideDistance * 0.35f;
                yield return null;
            }

            // 消える：トラッシュはスタンプが少し浮き上がりながら伝票ごと薄れる。ノートラッシュは伝票ごと下へずり落ちて消える。
            Vector2 from = _root.anchoredPosition;
            Vector3 fromScale = _stamp.localScale;
            float disappear = Mathf.Max(0.01f, _disappearSeconds);
            elapsed = 0f;
            while (elapsed < disappear)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / disappear);
                if (hasTrash)
                {
                    _stamp.localScale = fromScale * Mathf.Lerp(1f, 1.06f, t);
                }
                else
                {
                    _root.anchoredPosition = from + Vector2.down * _noTrashSlideDistance * 0.65f * t * t;
                }
                _rootGroup.alpha = 1f - t;
                yield return null;
            }

            Destroy(_root.gameObject);
            _root = null;
        }

        private void OnDestroy()
        {
            foreach (var material in _textMaterials.Values)
            {
                if (material != null)
                {
                    Destroy(material);
                }
            }
            _textMaterials.Clear();
        }

        private void Build(Vector2 slipSize, Vector2 stampSize, Color ink, bool hasTrash, int count,
            BonusAward? miracle, IReadOnlyList<string> bonusLines)
        {
            if (_root != null)
            {
                Destroy(_root.gameObject);
            }

            // ボーナスの行の分だけ伝票を下に伸ばし、そのぶん全体を上へずらす（伝票の下端の位置は変えず、盤面を隠さない）。
            float extra = bonusLines.Count > 0 ? bonusLines.Count * _bonusLineHeight + _bonusLineGap : 0f;
            var fullSlip = new Vector2(slipSize.x, slipSize.y + extra);

            _root = CreateRect("Result", transform, Vector2.zero);
            _root.anchoredPosition = _centerOffset + Vector2.up * extra;
            _rootGroup = AddGroup(_root.gameObject);

            BuildSlip(fullSlip);
            _slip.anchoredPosition = new Vector2(0f, -extra / 2f);

            // スタンプは伝票の見出しを避けて、少し下寄りに押す。
            _stamp = CreateRect("Stamp", _root, stampSize);
            _stamp.anchoredPosition = new Vector2(0f, -slipSize.y * 0.05f);
            _stampGroup = AddGroup(_stamp.gameObject);
            _stampGroup.alpha = 0f;

            // 二重線の枠（外側は太く、内側は細く）。線を何本かの線分に分けて、1本ごとにインクの濃さを少し変える。
            float outer = Mathf.Max(6f, stampSize.y * 0.055f);
            float inner = Mathf.Max(3f, stampSize.y * 0.02f);
            float gap = outer * 0.8f;
            AddFrame(_stamp, stampSize, outer, ink, hasTrash ? 7 : 5);
            AddFrame(_stamp, stampSize - Vector2.one * (outer + gap) * 2f, inner, ink, hasTrash ? 6 : 4);

            Vector2 innerSize = stampSize - Vector2.one * (outer + gap + inner) * 2f;
            if (miracle.HasValue)
            {
                AddText(_stamp, $"{miracle.Value.Name}！", ink, new Vector2(0f, innerSize.y * 0.15f),
                    new Vector2(innerSize.x * 0.94f, innerSize.y * 0.6f), innerSize.y * 0.48f);
                AddText(_stamp, $"+{miracle.Value.Points}", ink, new Vector2(0f, -innerSize.y * 0.30f),
                    new Vector2(innerSize.x * 0.6f, innerSize.y * 0.36f), innerSize.y * 0.32f);
            }
            else if (hasTrash)
            {
                AddText(_stamp, "トラッシュ！", ink, new Vector2(0f, innerSize.y * 0.15f),
                    new Vector2(innerSize.x * 0.94f, innerSize.y * 0.6f), innerSize.y * 0.48f);
                AddText(_stamp, $"×{count}枚", ink, new Vector2(0f, -innerSize.y * 0.30f),
                    new Vector2(innerSize.x * 0.6f, innerSize.y * 0.36f), innerSize.y * 0.32f);
            }
            else
            {
                AddText(_stamp, "ノートラッシュ…", ink, Vector2.zero,
                    new Vector2(innerSize.x * 0.94f, innerSize.y * 0.7f), innerSize.y * 0.38f);
            }

            // ボーナスの行（伝票に水平に、スタンプの下へ1行ずつ。スタンプと一緒に現れる）。
            if (bonusLines.Count > 0)
            {
                var linesRoot = CreateRect("BonusLines", _root, new Vector2(slipSize.x, bonusLines.Count * _bonusLineHeight));
                linesRoot.anchoredPosition = new Vector2(0f, _stamp.anchoredPosition.y - stampSize.y / 2f - _bonusLineGap - bonusLines.Count * _bonusLineHeight / 2f);
                var linesGroup = linesRoot.gameObject.AddComponent<CanvasGroup>();
                linesGroup.blocksRaycasts = false;
                linesGroup.interactable = false;
                linesGroup.alpha = 0f;
                _bonusLinesGroup = linesGroup;
                for (int i = 0; i < bonusLines.Count; i++)
                {
                    var label = CreateText("Bonus", linesRoot, _textFont, bonusLines[i], _bonusLineHeight * 0.72f, _bonusLineColor,
                        new Vector2(0f, (bonusLines.Count - 1) * _bonusLineHeight / 2f - i * _bonusLineHeight),
                        new Vector2(slipSize.x * 0.86f, _bonusLineHeight));
                    label.fontStyle = FontStyles.Bold;
                    label.enableAutoSizing = true;
                    label.fontSizeMin = _bonusLineHeight * 0.3f;
                    label.fontSizeMax = _bonusLineHeight * 0.72f;
                }
            }
            else
            {
                _bonusLinesGroup = null;
            }
        }

        /// <summary>伝票（影・アイボリーの紙・上部の「LUCKY TRASH BANK」・細い罫線）を作る。</summary>
        private void BuildSlip(Vector2 size)
        {
            _slip = CreateRect("Slip", _root, size);
            _slip.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-_slipTiltDegrees, _slipTiltDegrees));
            _slipGroup = AddGroup(_slip.gameObject);
            _slipGroup.alpha = 0f;

            var shadow = CreateImage("Shadow", _slip, _paperSprite, new Color(0f, 0f, 0f, _shadowAlpha), size);
            shadow.rectTransform.anchoredPosition = new Vector2(6f, -8f);

            var paper = CreateImage("Paper", _slip, _paperSprite, _paperColor, size);
            var edge = paper.gameObject.AddComponent<Outline>();
            edge.effectColor = _paperEdgeColor;
            edge.effectDistance = new Vector2(1.5f, -1.5f);

            float s = size.y / 300f;
            var header = CreateText("BankName", _slip, _headerFont, "LUCKY TRASH BANK", 18f * s, _slipInkColor,
                new Vector2(0f, size.y / 2f - 24f * s), new Vector2(size.x - 40f * s, 30f * s));
            header.characterSpacing = 12f;
            header.fontStyle = FontStyles.Bold;

            // 見出しの下と、伝票の下側に細い罫線。
            Color rule = new Color(_slipInkColor.r, _slipInkColor.g, _slipInkColor.b, 0.35f);
            CreateImage("Rule", _slip, null, rule, new Vector2(size.x - 40f * s, 2f))
                .rectTransform.anchoredPosition = new Vector2(0f, size.y / 2f - 44f * s);
            for (int i = 0; i < 2; i++)
            {
                CreateImage("Rule", _slip, null, rule, new Vector2(size.x - 40f * s, 1.5f))
                    .rectTransform.anchoredPosition = new Vector2(0f, -size.y / 2f + (22f + 18f * i) * s);
            }
        }

        private void AddFrame(RectTransform parent, Vector2 size, float thickness, Color ink, int segmentsPerSide)
        {
            var rect = CreateRect("Frame", parent, size);

            float halfW = size.x / 2f;
            float halfH = size.y / 2f;
            // 上・下の辺（角まで伸ばす）と、左・右の辺（上下の辺の内側）。
            AddSide(rect, new Vector2(-halfW, halfH - thickness / 2f), new Vector2(halfW, halfH - thickness / 2f), thickness, ink, segmentsPerSide);
            AddSide(rect, new Vector2(-halfW, -halfH + thickness / 2f), new Vector2(halfW, -halfH + thickness / 2f), thickness, ink, segmentsPerSide);
            AddSide(rect, new Vector2(-halfW + thickness / 2f, halfH - thickness), new Vector2(-halfW + thickness / 2f, -halfH + thickness), thickness, ink, Mathf.Max(2, segmentsPerSide / 2));
            AddSide(rect, new Vector2(halfW - thickness / 2f, halfH - thickness), new Vector2(halfW - thickness / 2f, -halfH + thickness), thickness, ink, Mathf.Max(2, segmentsPerSide / 2));
        }

        private void AddSide(RectTransform parent, Vector2 from, Vector2 to, float thickness, Color ink, int segments)
        {
            bool horizontal = Mathf.Abs(to.x - from.x) > Mathf.Abs(to.y - from.y);
            for (int i = 0; i < segments; i++)
            {
                Vector2 a = Vector2.Lerp(from, to, (float)i / segments);
                Vector2 b = Vector2.Lerp(from, to, (float)(i + 1) / segments);
                float length = (b - a).magnitude;
                var image = CreateImage("Line", parent, null, RandomInk(ink),
                    horizontal ? new Vector2(length, thickness) : new Vector2(thickness, length));
                image.rectTransform.anchoredPosition = (a + b) / 2f;
            }
        }

        private void AddText(RectTransform parent, string text, Color ink, Vector2 position, Vector2 size, float fontSize)
        {
            var label = CreateText("Text", parent, _textFont, text, fontSize, Color.white, position, size);
            label.fontStyle = FontStyles.Bold;
            label.enableAutoSizing = true;
            label.fontSizeMin = fontSize * 0.4f;
            label.fontSizeMax = fontSize;

            // 同じ色の縁取りで文字を太くする。
            var material = GetTextMaterial(label, ink);
            if (material != null)
            {
                label.fontSharedMaterial = material;
            }

            // 弱いかすれ：四隅の濃さを少しだけばらばらにした頂点グラデーション。
            label.enableVertexGradient = true;
            label.colorGradient = new VertexGradient(
                RandomInk(ink), RandomInk(ink), RandomInk(ink), RandomInk(ink));
        }

        private Material GetTextMaterial(TMP_Text label, Color ink)
        {
            if (_textOutlineWidth <= 0f || label.fontSharedMaterial == null)
            {
                return null;
            }
            if (_textMaterials.TryGetValue(ink, out var cached) && cached != null)
            {
                return cached;
            }

            var material = new Material(label.fontSharedMaterial) { name = "BankStampText" };
            material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, _textOutlineWidth);
            material.SetColor(ShaderUtilities.ID_OutlineColor, new Color(ink.r, ink.g, ink.b, 1f));
            _textMaterials[ink] = material;
            return material;
        }

        private TextMeshProUGUI CreateText(string name, Transform parent, TMP_FontAsset font, string text, float fontSize,
            Color color, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var label = go.GetComponent<TextMeshProUGUI>();
            if (font != null)
            {
                label.font = font;
            }
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            label.color = color;
            return label;
        }

        private Image CreateImage(string name, Transform parent, Sprite sprite, Color color, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            if (sprite != null)
            {
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 3f;
            }
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 size)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            return rect;
        }

        private static CanvasGroup AddGroup(GameObject go)
        {
            var group = go.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            return group;
        }

        private Color RandomInk(Color ink)
        {
            return new Color(ink.r, ink.g, ink.b, ink.a * Random.Range(1f - _inkUnevenness, 1f));
        }
    }
}
