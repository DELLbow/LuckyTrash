using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LuckyTrash.UI.Scoring;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LuckyTrash.UI
{
    /// <summary>明細書の1行（プレイヤー1人分）。</summary>
    public struct StatementRow
    {
        public int Rank;
        public Color MedalColor;
        public string Suit;
        public Color SuitColor;
        public string Name;
        public bool IsHuman;
        public int RankPoints;
        public IReadOnlyList<BonusAward> Bonuses;
        public int Deposit;
        public int PreviousBalance;
        public int Balance;
        /// <summary>通算が一番高い（王冠を付ける）。</summary>
        public bool IsLeader;
    }

    /// <summary>
    /// 順位が確定したあとの、ゲームのテーブルの上で見せる最後の演出の UI 部分。
    /// ・最下位の人のネームプレートに、伝票のスタンプと同じ赤いインクで「TRASH」を押す
    /// ・1位の人のネームプレートの金のメダルを光らせる
    /// ・演出の途中で画面のどこかをタップしたら飛ばすための、透明なタップ受け
    /// ・「LUCKY TRASH BANK」の取引明細書（今回の順位の順に、順位の得点・ボーナス・入金・残高。残高はドラムで回って増える）と、
    ///   その下の「もう一度」「人数を選び直す」のカジノチップ型ボタン
    /// 新しい画像素材は使わず、角丸の板・円・TextMeshPro で組み立てる。ボタンを押した後の処理（シーンの読み込みなど）は
    /// 呼び出し側から受け取る。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class GameFinaleView : MonoBehaviour
    {
        [Header("素材")]
        [Tooltip("角丸の板（9スライス）。Assets/Textures/RoundedPanel.png。")]
        [SerializeField] private Sprite _roundedSprite;
        [Tooltip("円（UI/Skin/Knob）。メダル・チップ・光に使う。")]
        [SerializeField] private Sprite _circleSprite;
        [Tooltip("日本語の文字のフォント（MSGothic SDF）。")]
        [SerializeField] private TMP_FontAsset _textFont;
        [Tooltip("英字・数字・♠♥♦♣のフォント（LiberationSans SDF - Fallback）。")]
        [SerializeField] private TMP_FontAsset _symbolFont;

        [Header("明細書の大きさ・位置（Canvasの参照解像度基準の単位）")]
        [SerializeField] private float _paperWidth = 1180f;
        [Tooltip("見出し（LUCKY TRASH BANK・取引明細・第N戦）の高さ。")]
        [SerializeField] private float _headerHeight = 130f;
        [Tooltip("列の名前の行の高さ。")]
        [SerializeField] private float _columnHeaderHeight = 40f;
        [Tooltip("プレイヤー1人分の行の高さ。")]
        [SerializeField] private float _rowHeight = 70f;
        [Tooltip("明細書の中心の、画面中央からのずれ（ボタンが下に収まるよう少し上）。")]
        [SerializeField] private float _paperCenterY = 40f;
        [Tooltip("明細書の後ろを暗くする濃さ（テーブルの上で読みやすくする）。")]
        [Range(0f, 1f)] [SerializeField] private float _backdropAlpha = 0.45f;

        [Header("ボタン")]
        [Tooltip("カジノチップ型ボタンの直径。")]
        [SerializeField] private float _chipDiameter = 140f;
        [Tooltip("2つのボタンの中心の間隔。")]
        [SerializeField] private float _chipSpacing = 260f;
        [Tooltip("明細書の下端からボタンの中心までの距離。")]
        [SerializeField] private float _chipOffsetBelowPaper = 95f;
        [SerializeField] private string _playAgainLabel = "もう一度";
        [SerializeField] private string _reselectLabel = "人数を\n選び直す";
        [Tooltip("「もう一度」のチップの色（赤）。")]
        [SerializeField] private Color _playAgainChipColor = new Color(0.72f, 0.09f, 0.10f);
        [Tooltip("「人数を選び直す」のチップの色（黒）。")]
        [SerializeField] private Color _reselectChipColor = new Color(0.10f, 0.10f, 0.11f);

        [Header("時間（秒）")]
        [Tooltip("明細書が下からすっと出てくる時間。")]
        [SerializeField] private float _paperEnterSeconds = 0.35f;
        [Tooltip("明細書が出てから、残高のドラムが回り始めるまでの時間。")]
        [SerializeField] private float _drumDelaySeconds = 0.3f;
        [Tooltip("残高のドラムが回る時間。")]
        [SerializeField] private float _drumRollSeconds = 0.9f;
        [Tooltip("行ごとにドラムの回り始めをずらす間隔。")]
        [SerializeField] private float _drumRowStagger = 0.15f;

        [Header("TRASH のスタンプ")]
        [SerializeField] private string _trashStampText = "TRASH";
        [SerializeField] private Color _trashInkColor = new Color(0.78f, 0.06f, 0.08f, 1f);
        [Tooltip("押し始めの大きさ（倍率）。ここから一気に縮んで押される。")]
        [SerializeField] private float _trashStampStartScale = 2.4f;
        [SerializeField] private float _trashStampSeconds = 0.18f;
        [Tooltip("スタンプの傾き（度）。")]
        [SerializeField] private float _trashStampTilt = -8f;
        [Tooltip("スタンプの下に敷く紙の色の濃さ（下の名前を薄く隠して、TRASH を読みやすくする）。")]
        [Range(0f, 1f)] [SerializeField] private float _trashStampBacking = 0.85f;

        [Header("色")]
        [SerializeField] private Color _paperColor = new Color(1f, 0.98f, 0.92f);
        [SerializeField] private Color _paperEdgeColor = new Color(0.55f, 0.52f, 0.46f, 0.9f);
        [Tooltip("見出し・列の名前・罫線の色。")]
        [SerializeField] private Color _slipInkColor = new Color(0.30f, 0.32f, 0.36f);
        [Tooltip("数字・名前の色（濃い墨色）。")]
        [SerializeField] private Color _numberColor = new Color(0.12f, 0.12f, 0.16f);
        [Tooltip("ボーナスの文字の色。")]
        [SerializeField] private Color _bonusColor = new Color(0.62f, 0.10f, 0.08f);
        [Tooltip("自分の行の背景（うっすら金色）。")]
        [SerializeField] private Color _humanRowColor = new Color(1f, 0.88f, 0.50f, 0.25f);
        [Tooltip("通算1位の王冠（★）の色。")]
        [SerializeField] private Color _crownColor = new Color(0.86f, 0.62f, 0.10f);
        [Tooltip("メダルが光るときの色。")]
        [SerializeField] private Color _medalGlowColor = new Color(1f, 0.85f, 0.35f, 0.9f);

        private RectTransform _skipCatcher;
        private RectTransform _statementRoot;
        private readonly List<GameObject> _effects = new List<GameObject>();
        private readonly Dictionary<Color, Material> _outlineMaterials = new Dictionary<Color, Material>();

        /// <summary>明細書を表示しているか。</summary>
        public bool IsShowingStatement => _statementRoot != null;

        // ------------------------------------------------------------------
        // タップで演出を飛ばす
        // ------------------------------------------------------------------

        /// <summary>画面全体を覆う透明なタップ受けを出し、タップされたら onTap を1回だけ呼ぶ。</summary>
        public void ShowSkipCatcher(Action onTap)
        {
            HideSkipCatcher();
            var go = new GameObject("SkipCatcher", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            _skipCatcher = (RectTransform)go.transform;
            _skipCatcher.SetParent(transform, false);
            Stretch(_skipCatcher);
            var image = go.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = true;
            var button = go.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() =>
            {
                HideSkipCatcher();
                onTap?.Invoke();
            });
        }

        public void HideSkipCatcher()
        {
            if (_skipCatcher != null)
            {
                Destroy(_skipCatcher.gameObject);
                _skipCatcher = null;
            }
        }

        // ------------------------------------------------------------------
        // ネームプレートへの演出
        // ------------------------------------------------------------------

        /// <summary>最下位の人のネームプレートに「TRASH」のスタンプをドンと押す（押し終わるまで待つ）。</summary>
        public IEnumerator StampTrash(RectTransform plate)
        {
            var stamp = BuildTrashStamp(plate);
            if (stamp == null)
            {
                yield break;
            }
            var group = stamp.GetComponent<CanvasGroup>();
            float duration = Mathf.Max(0.01f, _trashStampSeconds);
            float elapsed = 0f;
            while (elapsed < duration && stamp != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                stamp.localScale = Vector3.one * Mathf.Lerp(_trashStampStartScale, 0.94f, t * t);
                group.alpha = Mathf.Clamp01(t * 2.5f);
                yield return null;
            }
            // 押した瞬間に少しだけ跳ね返る（大げさに揺らさない）。
            elapsed = 0f;
            while (elapsed < 0.12f && stamp != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / 0.12f);
                stamp.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, 1f - (1f - t) * (1f - t));
                yield return null;
            }
            if (stamp != null)
            {
                stamp.localScale = Vector3.one;
                group.alpha = 1f;
            }
        }

        /// <summary>演出を飛ばしたときに、「TRASH」のスタンプを押した後の状態ですぐに置く。</summary>
        public void StampTrashImmediate(RectTransform plate)
        {
            var stamp = BuildTrashStamp(plate);
            if (stamp != null)
            {
                stamp.localScale = Vector3.one;
                stamp.GetComponent<CanvasGroup>().alpha = 1f;
            }
        }

        /// <summary>1位の人のネームプレートのメダルを、ふわっと数回光らせる。</summary>
        public IEnumerator GlowMedal(RectTransform medal, float seconds)
        {
            if (medal == null)
            {
                yield break;
            }
            var glow = CreateImage("MedalGlow", medal.parent, _circleSprite, new Color(_medalGlowColor.r, _medalGlowColor.g, _medalGlowColor.b, 0f));
            _effects.Add(glow.gameObject);
            var rect = glow.rectTransform;
            rect.anchorMin = medal.anchorMin;
            rect.anchorMax = medal.anchorMax;
            rect.pivot = medal.pivot;
            rect.anchoredPosition = medal.anchoredPosition;
            rect.sizeDelta = medal.sizeDelta * 1.9f;
            rect.SetSiblingIndex(medal.GetSiblingIndex());

            float elapsed = 0f;
            while (elapsed < seconds && glow != null)
            {
                elapsed += Time.deltaTime;
                float pulse = 0.5f - 0.5f * Mathf.Cos(elapsed / 0.6f * Mathf.PI * 2f);
                float fade = Mathf.Clamp01(Mathf.Min(elapsed / 0.15f, (seconds - elapsed) / 0.3f));
                glow.color = new Color(_medalGlowColor.r, _medalGlowColor.g, _medalGlowColor.b, _medalGlowColor.a * pulse * fade);
                rect.localScale = Vector3.one * (0.85f + 0.25f * pulse);
                medal.localScale = Vector3.one * (1f + 0.12f * pulse * fade);
                yield return null;
            }
            if (medal != null)
            {
                medal.localScale = Vector3.one;
            }
            if (glow != null)
            {
                _effects.Remove(glow.gameObject);
                Destroy(glow.gameObject);
            }
        }

        /// <summary>メダルの光など、途中の演出の部品を消す（演出を飛ばしたとき用。TRASH のスタンプは残す）。</summary>
        public void ClearTransientEffects()
        {
            foreach (var go in _effects)
            {
                if (go != null)
                {
                    Destroy(go);
                }
            }
            _effects.Clear();
        }

        private RectTransform BuildTrashStamp(RectTransform plate)
        {
            if (plate == null)
            {
                return null;
            }
            var old = plate.Find("TrashStamp");
            if (old != null)
            {
                Destroy(old.gameObject);
            }

            Vector2 plateSize = plate.rect.size;
            var size = new Vector2(plateSize.x * 0.64f, plateSize.y * 0.62f);
            var stamp = CreateRect("TrashStamp", plate, size);
            stamp.localRotation = Quaternion.Euler(0f, 0f, _trashStampTilt);
            var group = stamp.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            // 紙の色の下地（下の名前を薄く隠す）と、二重線の枠（伝票のスタンプと同じ赤いインク）。
            var backing = CreateImage("Backing", stamp, null, new Color(_paperColor.r, _paperColor.g, _paperColor.b, _trashStampBacking));
            backing.rectTransform.sizeDelta = size;
            float outer = Mathf.Max(3f, size.y * 0.08f);
            float inner = Mathf.Max(1.5f, size.y * 0.035f);
            AddFrame(stamp, size, outer, _trashInkColor);
            AddFrame(stamp, size - Vector2.one * outer * 3.2f, inner, _trashInkColor);

            var label = CreateText("Text", stamp, _symbolFont, _trashStampText, size.y * 0.5f, _trashInkColor,
                TextAlignmentOptions.Center);
            label.fontStyle = FontStyles.Bold;
            label.characterSpacing = 8f;
            label.rectTransform.sizeDelta = size * 0.86f;
            ApplyOutline(label, _trashInkColor, 0.18f);
            return stamp;
        }

        // ------------------------------------------------------------------
        // 取引明細書
        // ------------------------------------------------------------------

        /// <summary>
        /// 取引明細書と2つのボタンを表示する。残高は前回の残高から今回の残高へドラムで回って増え、
        /// 通算の1位が入れ替わったときは、その人の残高に「逆転！」の小さなスタンプを押す。
        /// </summary>
        public void ShowStatement(int matchNumber, IReadOnlyList<StatementRow> rows, bool leaderChanged,
            Action onPlayAgain, Action onReselect)
        {
            HideStatement();
            StartCoroutine(StatementRoutine(matchNumber, rows, leaderChanged, onPlayAgain, onReselect));
        }

        public void HideStatement()
        {
            if (_statementRoot != null)
            {
                Destroy(_statementRoot.gameObject);
                _statementRoot = null;
            }
        }

        private IEnumerator StatementRoutine(int matchNumber, IReadOnlyList<StatementRow> rows, bool leaderChanged,
            Action onPlayAgain, Action onReselect)
        {
            _statementRoot = CreateRect("Statement", transform, Vector2.zero);
            Stretch(_statementRoot);
            var rootGroup = _statementRoot.gameObject.AddComponent<CanvasGroup>();

            var backdrop = CreateImage("Backdrop", _statementRoot, null, new Color(0f, 0f, 0f, _backdropAlpha));
            Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = true;

            float paperHeight = _headerHeight + _columnHeaderHeight + rows.Count * _rowHeight + 28f;
            var paper = CreateRect("Paper", _statementRoot, new Vector2(_paperWidth, paperHeight));
            var paperRest = new Vector2(0f, _paperCenterY);
            paper.anchoredPosition = paperRest;
            BuildPaper(paper, paperHeight, matchNumber);

            var drums = new List<(BalanceDrum drum, RectTransform cell, StatementRow row)>();
            for (int i = 0; i < rows.Count; i++)
            {
                float y = paperHeight / 2f - _headerHeight - _columnHeaderHeight - _rowHeight * (i + 0.5f);
                drums.Add(BuildRow(paper, rows[i], y, i < rows.Count - 1));
            }

            // ボタン（明細書の下に並べる。ドラムが回り終わってから押せるようにする）。
            float chipY = paperRest.y - paperHeight / 2f - _chipOffsetBelowPaper;
            var playAgain = BuildChipButton(_statementRoot, _playAgainLabel, _playAgainChipColor, new Vector2(-_chipSpacing / 2f, chipY), onPlayAgain);
            var reselect = BuildChipButton(_statementRoot, _reselectLabel, _reselectChipColor, new Vector2(_chipSpacing / 2f, chipY), onReselect);
            foreach (var chip in new[] { playAgain, reselect })
            {
                chip.localScale = Vector3.zero;
            }

            // 明細書が下からすっと出てくる。
            float elapsed = 0f;
            float enter = Mathf.Max(0.01f, _paperEnterSeconds);
            while (elapsed < enter)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / enter);
                float eased = 1f - (1f - t) * (1f - t);
                paper.anchoredPosition = paperRest + Vector2.down * 80f * (1f - eased);
                rootGroup.alpha = eased;
                yield return null;
            }
            paper.anchoredPosition = paperRest;
            rootGroup.alpha = 1f;

            // 残高のドラムを、行ごとに少しずつずらして回す。
            yield return new WaitForSeconds(_drumDelaySeconds);
            float rollStart = Time.time;
            float total = _drumRollSeconds + _drumRowStagger * Mathf.Max(0, drums.Count - 1);
            while (Time.time - rollStart < total)
            {
                float now = Time.time - rollStart;
                for (int i = 0; i < drums.Count; i++)
                {
                    float t = Mathf.Clamp01((now - i * _drumRowStagger) / Mathf.Max(0.01f, _drumRollSeconds));
                    drums[i].drum.Apply(Mathf.SmoothStep(0f, 1f, t));
                }
                yield return null;
            }
            foreach (var d in drums)
            {
                d.drum.Apply(1f);
            }

            // 通算1位の王冠と、入れ替わったときの「逆転！」。
            foreach (var d in drums)
            {
                if (d.row.IsLeader)
                {
                    var crown = CreateText("Crown", d.cell, _textFont, "★", 30f, _crownColor, TextAlignmentOptions.Center);
                    crown.rectTransform.sizeDelta = new Vector2(34f, 34f);
                    crown.rectTransform.anchoredPosition = new Vector2(d.cell.sizeDelta.x / 2f + 22f, 2f);
                    if (leaderChanged)
                    {
                        StartCoroutine(StampReversal(d.cell));
                    }
                }
            }

            // ボタンがぽんと出る。
            elapsed = 0f;
            while (elapsed < 0.25f)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / 0.25f);
                float s = t < 0.7f ? Mathf.Lerp(0f, 1.08f, t / 0.7f) : Mathf.Lerp(1.08f, 1f, (t - 0.7f) / 0.3f);
                playAgain.localScale = reselect.localScale = Vector3.one * s;
                yield return null;
            }
            playAgain.localScale = reselect.localScale = Vector3.one;
        }

        private void BuildPaper(RectTransform paper, float height, int matchNumber)
        {
            var size = new Vector2(_paperWidth, height);
            var shadow = CreateImage("Shadow", paper, _roundedSprite, new Color(0f, 0f, 0f, 0.35f));
            shadow.type = Image.Type.Sliced;
            shadow.pixelsPerUnitMultiplier = 3f;
            shadow.rectTransform.sizeDelta = size;
            shadow.rectTransform.anchoredPosition = new Vector2(8f, -10f);

            var sheet = CreateImage("Sheet", paper, _roundedSprite, _paperColor);
            sheet.type = Image.Type.Sliced;
            sheet.pixelsPerUnitMultiplier = 3f;
            sheet.rectTransform.sizeDelta = size;
            var edge = sheet.gameObject.AddComponent<Outline>();
            edge.effectColor = _paperEdgeColor;
            edge.effectDistance = new Vector2(1.5f, -1.5f);

            float top = height / 2f;
            var bank = CreateText("BankName", paper, _symbolFont, "LUCKY TRASH BANK", 20f, _slipInkColor, TextAlignmentOptions.Center);
            bank.fontStyle = FontStyles.Bold;
            bank.characterSpacing = 14f;
            bank.rectTransform.sizeDelta = new Vector2(_paperWidth - 80f, 30f);
            bank.rectTransform.anchoredPosition = new Vector2(0f, top - 30f);

            var title = CreateText("Title", paper, _textFont, "取引明細", 38f, _numberColor, TextAlignmentOptions.Left);
            title.fontStyle = FontStyles.Bold;
            title.rectTransform.sizeDelta = new Vector2(400f, 50f);
            title.rectTransform.anchoredPosition = new Vector2(-_paperWidth / 2f + 40f + 200f, top - 82f);

            var match = CreateText("Match", paper, _textFont, $"第{matchNumber}戦", 32f, _numberColor, TextAlignmentOptions.Right);
            match.fontStyle = FontStyles.Bold;
            match.rectTransform.sizeDelta = new Vector2(300f, 50f);
            match.rectTransform.anchoredPosition = new Vector2(_paperWidth / 2f - 40f - 150f, top - 82f);

            Color rule = new Color(_slipInkColor.r, _slipInkColor.g, _slipInkColor.b, 0.55f);
            Rule(paper, top - _headerHeight + 6f, 2f, rule);

            // 列の名前。
            float headerY = top - _headerHeight - _columnHeaderHeight / 2f + 4f;
            ColumnLabel(paper, "順位", ColMedal, headerY, 90f, TextAlignmentOptions.Center);
            ColumnLabel(paper, "名前", ColNameLeft + 140f, headerY, 280f, TextAlignmentOptions.Left);
            ColumnLabel(paper, "順位点", ColRankRight - 65f, headerY, 130f, TextAlignmentOptions.Right);
            ColumnLabel(paper, "ボーナス", ColBonusLeft + 150f, headerY, 300f, TextAlignmentOptions.Left);
            ColumnLabel(paper, "入金", ColDepositRight - 60f, headerY, 120f, TextAlignmentOptions.Right);
            ColumnLabel(paper, "残高", ColBalanceRight - 75f, headerY, 150f, TextAlignmentOptions.Right);
            Rule(paper, top - _headerHeight - _columnHeaderHeight + 2f, 1.5f, rule);
        }

        // 列の位置（明細書の左端からの距離）。
        private const float ColMedal = 75f;
        private const float ColNameLeft = 120f;
        private const float ColRankRight = 590f;
        private const float ColBonusLeft = 630f;
        private const float ColDepositRight = 1010f;
        private const float ColBalanceRight = 1120f;

        private float X(float fromLeft) => -_paperWidth / 2f + fromLeft;

        private void ColumnLabel(RectTransform paper, string text, float centerFromLeft, float y, float width, TextAlignmentOptions align)
        {
            var label = CreateText("Col", paper, _textFont, text, 20f, _slipInkColor, align);
            label.rectTransform.sizeDelta = new Vector2(width, 28f);
            label.rectTransform.anchoredPosition = new Vector2(X(centerFromLeft), y);
        }

        private void Rule(RectTransform paper, float y, float thickness, Color color)
        {
            var line = CreateImage("Rule", paper, null, color);
            line.rectTransform.sizeDelta = new Vector2(_paperWidth - 70f, thickness);
            line.rectTransform.anchoredPosition = new Vector2(0f, y);
        }

        private (BalanceDrum, RectTransform, StatementRow) BuildRow(RectTransform paper, StatementRow row, float y, bool ruleBelow)
        {
            if (row.IsHuman)
            {
                var band = CreateImage("HumanBand", paper, null, _humanRowColor);
                band.rectTransform.sizeDelta = new Vector2(_paperWidth - 60f, _rowHeight - 6f);
                band.rectTransform.anchoredPosition = new Vector2(0f, y);
            }

            // 順位のメダル（ネームプレートのメダルと同じ色の丸に数字）。
            float medalSize = 46f;
            var medal = CreateRect("Medal", paper, Vector2.one * medalSize);
            medal.anchoredPosition = new Vector2(X(ColMedal), y);
            var disc = CreateImage("Disc", medal, _circleSprite, row.MedalColor);
            disc.rectTransform.sizeDelta = Vector2.one * medalSize;
            var discEdge = disc.gameObject.AddComponent<Outline>();
            discEdge.effectColor = Color.Lerp(row.MedalColor, Color.black, 0.45f);
            discEdge.effectDistance = new Vector2(1.5f, -1.5f);
            var ring = CreateImage("Ring", medal, _circleSprite, Color.Lerp(row.MedalColor, Color.white, 0.45f));
            ring.rectTransform.sizeDelta = Vector2.one * medalSize * 0.72f;
            var inner = CreateImage("Inner", medal, _circleSprite, row.MedalColor);
            inner.rectTransform.sizeDelta = Vector2.one * medalSize * 0.62f;
            var number = CreateText("Rank", medal, _symbolFont, row.Rank.ToString(), medalSize * 0.5f,
                Color.Lerp(row.MedalColor, Color.black, 0.65f), TextAlignmentOptions.Center);
            number.fontStyle = FontStyles.Bold;
            number.rectTransform.sizeDelta = Vector2.one * medalSize;

            // スート記号と名前（ネームプレートと同じ）。
            var suit = CreateText("Suit", paper, _symbolFont, row.Suit, 34f, row.SuitColor, TextAlignmentOptions.Center);
            suit.rectTransform.sizeDelta = new Vector2(40f, _rowHeight);
            suit.rectTransform.anchoredPosition = new Vector2(X(ColNameLeft + 20f), y);
            var name = CreateText("Name", paper, _textFont, row.Name, 30f, _numberColor, TextAlignmentOptions.Left);
            name.enableAutoSizing = true;
            name.fontSizeMin = 18f;
            name.fontSizeMax = 30f;
            name.rectTransform.sizeDelta = new Vector2(250f, _rowHeight);
            name.rectTransform.anchoredPosition = new Vector2(X(ColNameLeft + 50f + 125f), y);

            NumberCell(paper, row.RankPoints.ToString(), ColRankRight, y, 130f, 32f);

            // ボーナス（名前と点数。同じボーナスが複数回付いたら「×2」とまとめる。なければ「―」）。
            var bonus = CreateText("Bonus", paper, _textFont, FormatBonuses(row.Bonuses), 22f,
                row.Bonuses != null && row.Bonuses.Count > 0 ? _bonusColor : _slipInkColor, TextAlignmentOptions.Left);
            bonus.enableAutoSizing = true;
            bonus.fontSizeMin = 14f;
            bonus.fontSizeMax = 22f;
            bonus.lineSpacing = -12f;
            bonus.rectTransform.sizeDelta = new Vector2(300f, _rowHeight - 8f);
            bonus.rectTransform.anchoredPosition = new Vector2(X(ColBonusLeft + 150f), y);

            NumberCell(paper, "+" + row.Deposit, ColDepositRight, y, 120f, 32f);

            // 残高（ドラムで回って増える）。
            int digits = Mathf.Max(3, Mathf.Max(row.Balance, row.PreviousBalance).ToString().Length);
            var cell = CreateRect("Balance", paper, new Vector2(150f, _rowHeight));
            cell.anchoredPosition = new Vector2(X(ColBalanceRight - 75f), y);
            var drum = new BalanceDrum(this, cell, digits, row.PreviousBalance, row.Balance, 36f);

            if (ruleBelow)
            {
                Rule(paper, y - _rowHeight / 2f, 1f, new Color(_slipInkColor.r, _slipInkColor.g, _slipInkColor.b, 0.25f));
            }
            return (drum, cell, row);
        }

        private void NumberCell(RectTransform paper, string text, float rightFromLeft, float y, float width, float size)
        {
            var label = CreateText("Number", paper, _symbolFont, text, size, _numberColor, TextAlignmentOptions.Right);
            label.fontStyle = FontStyles.Bold;
            label.rectTransform.sizeDelta = new Vector2(width, _rowHeight);
            label.rectTransform.anchoredPosition = new Vector2(X(rightFromLeft - width / 2f), y);
        }

        /// <summary>ボーナスの表示（同じ種類は「×回数」でまとめ、1行に1種類。なければ「―」）。</summary>
        public static string FormatBonuses(IReadOnlyList<BonusAward> bonuses)
        {
            if (bonuses == null || bonuses.Count == 0)
            {
                return "―";
            }
            var lines = bonuses
                .GroupBy(b => b.Kind)
                .Select(g =>
                {
                    var first = g.First();
                    int count = g.Count();
                    int points = g.Sum(b => b.Points);
                    return count > 1 ? $"{first.Name}×{count} +{points}" : $"{first.Name} +{points}";
                });
            return string.Join("\n", lines);
        }

        private IEnumerator StampReversal(RectTransform cell)
        {
            var size = new Vector2(96f, 40f);
            var stamp = CreateRect("Reversal", cell, size);
            // 王冠（★）のさらに右、明細書の右端から少しはみ出すように押す（入金・残高の数字にかからない）。
            stamp.anchoredPosition = new Vector2(cell.sizeDelta.x / 2f + 95f, 0f);
            stamp.localRotation = Quaternion.Euler(0f, 0f, 10f);
            var group = stamp.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            AddFrame(stamp, size, 3f, _trashInkColor);
            var backing = CreateImage("Backing", stamp, null, new Color(_paperColor.r, _paperColor.g, _paperColor.b, 0.9f));
            backing.rectTransform.sizeDelta = size;
            backing.transform.SetAsFirstSibling();
            // 独自の縁取りマテリアルは使わない（後から追加された文字は別のアトラスに入り、そのマテリアルでは描けないため）。
            var label = CreateText("Text", stamp, _textFont, "逆転！", 24f, _trashInkColor, TextAlignmentOptions.Center);
            label.fontStyle = FontStyles.Bold;
            label.rectTransform.sizeDelta = size;

            float elapsed = 0f;
            while (elapsed < 0.18f && stamp != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / 0.18f);
                stamp.localScale = Vector3.one * Mathf.Lerp(2.2f, 1f, t * t);
                group.alpha = Mathf.Clamp01(t * 2.5f);
                yield return null;
            }
            if (stamp != null)
            {
                stamp.localScale = Vector3.one;
                group.alpha = 1f;
            }
        }

        /// <summary>右下の「回す」「引く」と同じ見た目のカジノチップ型ボタン。</summary>
        private RectTransform BuildChipButton(RectTransform parent, string text, Color chipColor, Vector2 position, Action onClick)
        {
            float d = _chipDiameter;
            var root = new GameObject("Chip_" + text.Replace("\n", ""), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            var rect = (RectTransform)root.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = Vector2.one * d;
            rect.anchoredPosition = position;

            var hit = root.GetComponent<Image>();
            hit.sprite = _circleSprite;
            hit.color = new Color(0f, 0f, 0f, 0.35f);
            hit.raycastTarget = true;
            hit.rectTransform.sizeDelta = Vector2.one * d;

            var visual = CreateRect("Visual", rect, Vector2.one * d);
            visual.anchoredPosition = new Vector2(0f, 4f);
            CreateImage("Body", visual, _circleSprite, chipColor).rectTransform.sizeDelta = Vector2.one * d;
            var ivory = new Color(1f, 0.98f, 0.92f);
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f;
                var notch = CreateImage($"Notch_{i}", visual, null, ivory);
                var r = notch.rectTransform;
                r.sizeDelta = new Vector2(d * 0.09f, d * 0.10f);
                r.localRotation = Quaternion.Euler(0f, 0f, -angle);
                r.anchoredPosition = Quaternion.Euler(0f, 0f, -angle) * new Vector3(0f, d * 0.44f, 0f);
            }
            CreateImage("InnerRing", visual, _circleSprite, new Color(1f, 0.96f, 0.86f)).rectTransform.sizeDelta = Vector2.one * d * 0.74f;
            CreateImage("InnerField", visual, _circleSprite, Color.Lerp(chipColor, Color.black, 0.12f)).rectTransform.sizeDelta = Vector2.one * d * 0.69f;

            var label = CreateText("Label", visual, _textFont, text, d * 0.17f, Color.white, TextAlignmentOptions.Center);
            label.fontStyle = FontStyles.Bold;
            label.enableAutoSizing = true;
            label.fontSizeMin = d * 0.08f;
            label.fontSizeMax = text.Contains("\n") ? d * 0.14f : d * 0.17f;
            label.lineSpacing = 0f;
            // 内側の地（直径の69%）に収まる広さ。
            label.rectTransform.sizeDelta = new Vector2(d * 0.56f, d * 0.46f);
            label.outlineWidth = 0.22f;
            label.outlineColor = Color.Lerp(chipColor, Color.black, 0.7f);

            var button = root.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            bool pressed = false;
            button.onClick.AddListener(() =>
            {
                if (pressed)
                {
                    return;
                }
                pressed = true;
                StartCoroutine(PressThen(visual, onClick));
            });
            return rect;
        }

        private IEnumerator PressThen(RectTransform visual, Action onClick)
        {
            float elapsed = 0f;
            while (elapsed < 0.12f && visual != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / 0.12f);
                visual.localScale = Vector3.one * (1f - 0.08f * Mathf.Sin(t * Mathf.PI));
                yield return null;
            }
            onClick?.Invoke();
        }

        // ------------------------------------------------------------------
        // 残高のドラム（山札表示と同じ回り方。各桁の回転量は DeckCountDrum で決める）
        // ------------------------------------------------------------------

        private sealed class BalanceDrum
        {
            private readonly RectTransform[] _strips;
            private readonly RectTransform[] _windows;
            private readonly int[] _fromDigits;
            private readonly int[] _steps;
            private readonly float _digitHeight;
            private readonly int _from;
            private readonly int _to;

            public BalanceDrum(GameFinaleView view, RectTransform cell, int digitCount, int from, int to, float fontSize)
            {
                _from = from;
                _to = to;
                _digitHeight = fontSize * 1.1f;
                float digitWidth = fontSize * 0.6f;
                _steps = DeckCountDrum.GetSteps(from, to, digitCount);
                _fromDigits = new int[digitCount];
                _strips = new RectTransform[digitCount];
                _windows = new RectTransform[digitCount];
                float rightEdge = cell.sizeDelta.x / 2f;
                for (int i = 0; i < digitCount; i++)
                {
                    int place = digitCount - 1 - i;
                    _fromDigits[i] = DeckCountDrum.GetDigit(from, place);
                    var window = new GameObject($"Digit_{i}", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
                    window.SetParent(cell, false);
                    window.sizeDelta = new Vector2(digitWidth, _digitHeight);
                    window.anchoredPosition = new Vector2(rightEdge - digitWidth * (digitCount - i - 0.5f), 0f);
                    _windows[i] = window;

                    var strip = new GameObject("Strip", typeof(RectTransform)).GetComponent<RectTransform>();
                    strip.SetParent(window, false);
                    strip.anchorMin = strip.anchorMax = new Vector2(0.5f, 1f);
                    strip.pivot = new Vector2(0.5f, 1f);
                    strip.sizeDelta = new Vector2(digitWidth, _digitHeight * 11f);
                    _strips[i] = strip;
                    // 0〜9 と、もう一度 0（9 から 0 へ回るときにつなげるため）。
                    for (int k = 0; k <= 10; k++)
                    {
                        var label = view.CreateText($"N{k}", strip, view._symbolFont, (k % 10).ToString(), fontSize, view._numberColor,
                            TextAlignmentOptions.Center);
                        label.fontStyle = FontStyles.Bold;
                        var lr = label.rectTransform;
                        lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 1f);
                        lr.pivot = new Vector2(0.5f, 1f);
                        lr.sizeDelta = new Vector2(digitWidth, _digitHeight);
                        lr.anchoredPosition = new Vector2(0f, -k * _digitHeight);
                    }
                }
                Apply(0f);
            }

            /// <summary>t（0〜1）まで回した状態にする。上の桁の0は表示しない。</summary>
            public void Apply(float t)
            {
                int shown = Mathf.RoundToInt(Mathf.Lerp(_from, _to, t));
                int length = Mathf.Max(1, shown.ToString().Length);
                for (int i = 0; i < _strips.Length; i++)
                {
                    float position = _fromDigits[i] + _steps[i] * t;
                    float wrapped = position - Mathf.Floor(position / 10f) * 10f;
                    _strips[i].anchoredPosition = new Vector2(0f, wrapped * _digitHeight);
                    int place = _strips.Length - 1 - i;
                    _windows[i].gameObject.SetActive(place < length);
                }
            }
        }

        // ------------------------------------------------------------------
        // 部品
        // ------------------------------------------------------------------

        private void AddFrame(RectTransform parent, Vector2 size, float thickness, Color color)
        {
            float hw = size.x / 2f;
            float hh = size.y / 2f;
            Line(parent, new Vector2(0f, hh - thickness / 2f), new Vector2(size.x, thickness), color);
            Line(parent, new Vector2(0f, -hh + thickness / 2f), new Vector2(size.x, thickness), color);
            Line(parent, new Vector2(-hw + thickness / 2f, 0f), new Vector2(thickness, size.y), color);
            Line(parent, new Vector2(hw - thickness / 2f, 0f), new Vector2(thickness, size.y), color);
        }

        private void Line(RectTransform parent, Vector2 position, Vector2 size, Color color)
        {
            var line = CreateImage("Line", parent, null, color);
            line.rectTransform.sizeDelta = size;
            line.rectTransform.anchoredPosition = position;
        }

        private void ApplyOutline(TMP_Text label, Color color, float width)
        {
            if (label.fontSharedMaterial == null)
            {
                return;
            }
            if (!_outlineMaterials.TryGetValue(color, out var material) || material == null)
            {
                material = new Material(label.fontSharedMaterial) { name = "FinaleStampText" };
                material.EnableKeyword(ShaderUtilities.Keyword_Outline);
                material.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
                material.SetColor(ShaderUtilities.ID_OutlineColor, color);
                _outlineMaterials[color] = material;
            }
            label.fontSharedMaterial = material;
        }

        private void OnDestroy()
        {
            foreach (var material in _outlineMaterials.Values)
            {
                if (material != null)
                {
                    Destroy(material);
                }
            }
            _outlineMaterials.Clear();
        }

        private TextMeshProUGUI CreateText(string name, Transform parent, TMP_FontAsset font, string text, float size, Color color,
            TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<TextMeshProUGUI>();
            if (font != null)
            {
                label.font = font;
            }
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = align;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            return label;
        }

        private static Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
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

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
