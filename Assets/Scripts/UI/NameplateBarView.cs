using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace LuckyTrash.UI
{
    /// <summary>
    /// 画面左上に、参加プレイヤーのネームプレートを手番の順（1P, 2P, ...）に横一列で並べるビュー。
    /// Screen Space Overlay の Canvas 直下に置き、このRectTransformを左上に固定して使う。
    /// 各プレートの中身は <see cref="NameplateView"/> がデザイン（<see cref="Design"/>）に応じてコードで組み立てる
    /// （新しい画像素材は使わず、Image・TextMeshPro・Outline/Shadow と既存のスプライトだけで作る）。
    /// 盤面の3D座標とは無関係なので、カメラがパン・ズームしても盤面の上に名前が浮くことはない。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class NameplateBarView : MonoBehaviour
    {
        /// <summary>ネームプレートの見た目の案。</summary>
        public enum Design
        {
            A_PlayingCard,
            B_CasinoChip,
            C_DealerPlate
        }

        [Header("デザイン")]
        [SerializeField] private Design _design = Design.A_PlayingCard;

        [Header("配置（Canvasの参照解像度基準の単位）")]
        [Tooltip("プレート1枚の大きさ。")]
        [SerializeField] private Vector2 _plateSize = new Vector2(250f, 78f);
        [Tooltip("プレート同士の間隔。")]
        [SerializeField] private float _spacing = 14f;
        [Tooltip("画面の左端・上端からの余白。")]
        [SerializeField] private Vector2 _screenMargin = new Vector2(24f, 20f);

        [Header("手番の強調")]
        [Tooltip("手番のプレートの拡大率。")]
        [SerializeField] private float _highlightScale = 1.08f;
        [Tooltip("強調の切り替えにかける時間（秒、タイムスケールの影響を受けない）。")]
        [SerializeField] private float _highlightDuration = 0.2f;
        [Tooltip("デザインごとの強調表現の強さ（A: 持ち上がる量と影、B: チップの光、C: 金の縁の太さと明るさ）。")]
        [Range(0f, 2f)] [SerializeField] private float _highlightStrength = 1f;

        [Header("追加情報（任意）")]
        [Tooltip("手札の残り枚数を表示する。")]
        [SerializeField] private bool _showHandCount;
        [Tooltip("上がったプレイヤーに順位のバッジを付け、プレートを暗くする。")]
        [SerializeField] private bool _showRank;
        [Tooltip("上がったプレイヤーのプレートを暗くする量（0=そのまま、1=真っ黒。_showRank が有効なときのみ）。" +
            "半透明にするとリニア色空間では下の縁取りや背景が透けるため、黒い板を上に重ねて暗くする。")]
        [Range(0f, 0.9f)] [SerializeField] private float _finishedDim = 0.45f;

        [Header("ミニテーブル図（座席の位置）")]
        [Tooltip("テーブル（角丸の四角）の大きさ。座席の丸はこの4辺の中央に、半分はみ出して並ぶ。")]
        [SerializeField] private Vector2 _miniTableSize = new Vector2(36f, 26f);
        [Tooltip("座席の丸の直径。")]
        [SerializeField] private float _seatDotSize = 12f;
        [Tooltip("テーブルの色。")]
        [SerializeField] private Color _miniTableColor = new Color(0.18f, 0.46f, 0.30f);
        [Tooltip("ほかのプレイヤーの座席の丸の中の色（輪郭だけに見えるよう、プレートの地に近い色）。")]
        [SerializeField] private Color _otherSeatFillColor = new Color(1f, 0.98f, 0.92f);
        [Tooltip("ほかのプレイヤーの座席の丸の輪郭の色。")]
        [SerializeField] private Color _otherSeatOutlineColor = new Color(0.40f, 0.38f, 0.35f);
        [Tooltip("参加していない座席（2人・3人対戦で使わない席）の丸の不透明度。0で表示しない。")]
        [Range(0f, 1f)] [SerializeField] private float _unusedSeatAlpha = 0f;

        [Header("色")]
        [Tooltip("♥♦の赤。プレート左上のスート記号と、ミニテーブル図の自分の席の塗りつぶしに使う。")]
        [SerializeField] private Color _suitRedColor = new Color(0.80f, 0.10f, 0.12f);
        [Tooltip("♠♣の黒。プレート左上のスート記号と、ミニテーブル図の自分の席の塗りつぶしに使う。")]
        [SerializeField] private Color _suitBlackColor = new Color(0.10f, 0.10f, 0.12f);

        [Header("素材")]
        [Tooltip("角丸の板（9スライス）。Assets/Textures/RoundedPanel.png。")]
        [SerializeField] private Sprite _roundedSprite;
        [Tooltip("円（カジノチップ用）。Unity標準のUIスプライト（UI/Skin/Knob）。")]
        [SerializeField] private Sprite _circleSprite;
        [Tooltip("名前・「1P」などの文字のフォント（日本語を含む）。")]
        [SerializeField] private TMP_FontAsset _textFont;
        [Tooltip("♠♥♦♣の記号のフォント。日本語フォントが記号を持たない環境でも表示できるよう、" +
            "ソースフォントを同梱した LiberationSans SDF - Fallback を使う。")]
        [SerializeField] private TMP_FontAsset _symbolFont;

        private readonly List<NameplateView> _plates = new List<NameplateView>();
        private List<NameplateInfo> _infos;
        private readonly Dictionary<int, (int HandCount, int? Rank)> _states = new Dictionary<int, (int, int?)>();
        private int _currentSeatIndex = -1;
        private bool _rebuildRequested;

        public Design CurrentDesign => _design;
        public Vector2 PlateSize => _plateSize;
        public float HighlightScale => _highlightScale;
        public float HighlightDuration => _highlightDuration;
        public float HighlightStrength => _highlightStrength;
        public bool ShowHandCount => _showHandCount;
        public bool ShowRank => _showRank;
        public float FinishedDim => _finishedDim;
        public Sprite RoundedSprite => _roundedSprite;
        public Sprite CircleSprite => _circleSprite;
        public TMP_FontAsset TextFont => _textFont;
        public TMP_FontAsset SymbolFont => _symbolFont;
        public Vector2 MiniTableSize => _miniTableSize;
        public float SeatDotSize => _seatDotSize;
        public Color MiniTableColor => _miniTableColor;
        public Color OtherSeatFillColor => _otherSeatFillColor;
        public Color OtherSeatOutlineColor => _otherSeatOutlineColor;
        public float UnusedSeatAlpha => _unusedSeatAlpha;
        public Color SuitRedColor => _suitRedColor;
        public Color SuitBlackColor => _suitBlackColor;

        /// <summary>
        /// 手番の順に並べたプレイヤー情報からプレートを作り直す（試合開始時に1回呼ぶ）。
        /// </summary>
        public void Build(IReadOnlyList<NameplateInfo> infosInTurnOrder)
        {
            _infos = new List<NameplateInfo>(infosInTurnOrder);
            Rebuild();
        }

        /// <summary>手番のプレイヤーを設定し、そのプレートを強調する（-1で強調なし）。</summary>
        public void SetCurrentPlayer(int seatIndex)
        {
            _currentSeatIndex = seatIndex;
            foreach (var plate in _plates)
            {
                plate.SetHighlighted(plate.SeatIndex == seatIndex);
            }
        }

        /// <summary>手札の枚数・順位を更新する（順位は上がっていなければ null）。</summary>
        public void SetPlayerState(int seatIndex, int handCount, int? rank)
        {
            _states[seatIndex] = (handCount, rank);
            foreach (var plate in _plates)
            {
                if (plate.SeatIndex == seatIndex)
                {
                    plate.SetState(handCount, rank);
                }
            }
        }

        /// <summary>デザインを切り替えて作り直す（見た目の比較用）。</summary>
        public void SetDesign(Design design)
        {
            _design = design;
            Rebuild();
        }

        /// <summary>任意の追加情報（残り枚数・順位）の表示を切り替えて作り直す（見た目の比較用）。</summary>
        public void SetOptionalInfo(bool showHandCount, bool showRank)
        {
            _showHandCount = showHandCount;
            _showRank = showRank;
            Rebuild();
        }

        [ContextMenu("Rebuild")]
        private void Rebuild()
        {
            _rebuildRequested = false;

            foreach (var plate in _plates)
            {
                if (plate != null)
                {
                    Destroy(plate.gameObject);
                }
            }
            _plates.Clear();

            var rect = (RectTransform)transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(_screenMargin.x, -_screenMargin.y);

            if (_infos == null)
            {
                rect.sizeDelta = Vector2.zero;
                return;
            }

            rect.sizeDelta = new Vector2(
                _infos.Count * _plateSize.x + Mathf.Max(0, _infos.Count - 1) * _spacing, _plateSize.y);

            // ミニテーブル図に丸を出す座席（＝参加しているプレイヤーの座席）。
            var occupied = new HashSet<NameplateSeatDirection>();
            foreach (var info in _infos)
            {
                occupied.Add(info.Direction);
            }

            for (int i = 0; i < _infos.Count; i++)
            {
                var go = new GameObject($"Nameplate_{_infos[i].OrderNumber}P", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                var plateRect = (RectTransform)go.transform;
                plateRect.anchorMin = new Vector2(0f, 1f);
                plateRect.anchorMax = new Vector2(0f, 1f);
                plateRect.pivot = new Vector2(0.5f, 0.5f);
                plateRect.sizeDelta = _plateSize;
                plateRect.anchoredPosition = new Vector2(
                    i * (_plateSize.x + _spacing) + _plateSize.x / 2f, -_plateSize.y / 2f);

                var plate = go.AddComponent<NameplateView>();
                plate.Build(this, _infos[i], occupied);
                if (_states.TryGetValue(_infos[i].SeatIndex, out var state))
                {
                    plate.SetState(state.HandCount, state.Rank);
                }
                plate.SetHighlighted(_infos[i].SeatIndex == _currentSeatIndex, immediate: true);
                _plates.Add(plate);
            }
        }

        private void OnValidate()
        {
            // 再生中にInspectorで値を変えたら、次のフレームで作り直して見た目に反映する。
            if (Application.isPlaying && _infos != null)
            {
                _rebuildRequested = true;
            }
        }

        private void Update()
        {
            if (_rebuildRequested)
            {
                Rebuild();
            }
        }
    }

    /// <summary>ネームプレート1枚分の表示内容。</summary>
    public readonly struct NameplateInfo
    {
        /// <summary>手番の順番（1始まり、「1P」の数字）。</summary>
        public int OrderNumber { get; }
        public int SeatIndex { get; }
        public string DisplayName { get; }
        /// <summary>そのプレイヤーの手札が、通常のカメラから見て画面上のどちらにあるか（<see cref="MiniTableSeats.ToScreenPosition"/>）。</summary>
        public NameplateSeatDirection Direction { get; }
        /// <summary>人間のプレイヤーか（プレートに「あなた」と表示する）。</summary>
        public bool IsHuman { get; }

        public NameplateInfo(int orderNumber, int seatIndex, string displayName, NameplateSeatDirection direction, bool isHuman)
        {
            OrderNumber = orderNumber;
            SeatIndex = seatIndex;
            DisplayName = displayName;
            Direction = direction;
            IsHuman = isHuman;
        }
    }

    /// <summary>手札の位置を、通常のカメラから見た画面上の方向で表したもの。</summary>
    public enum NameplateSeatDirection
    {
        /// <summary>手前（自分の座席）。</summary>
        Down,
        Left,
        Up,
        Right
    }
}
