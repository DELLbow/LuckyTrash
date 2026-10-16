using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LuckyTrash.UI
{
    /// <summary>
    /// ネームプレート1枚。<see cref="NameplateBarView"/> が実行時に生成し、選ばれたデザインに応じて
    /// Image・TextMeshPro・Outline だけで中身を組み立てる。手番の強調は 0〜1 の強さを
    /// タイムスケールに依存せず補間し、デザインごとの表現（持ち上がる・光る・縁が太くなる）に反映する。
    /// </summary>
    public class NameplateView : MonoBehaviour
    {
        // 1P=♠, 2P=♥, 3P=♦, 4P=♣（案A）。
        private static readonly string[] Suits = { "♠", "♥", "♦", "♣" };

        // 1P〜4P のチップの色（案B）。
        private static readonly Color[] ChipColors =
        {
            new Color(0.78f, 0.14f, 0.14f),
            new Color(0.16f, 0.34f, 0.78f),
            new Color(0.10f, 0.52f, 0.26f),
            new Color(0.16f, 0.16f, 0.18f)
        };

        // ルーレットの黒と金の縁にそろえた色（案C）。
        private static readonly Color Gold = new Color(0.83f, 0.68f, 0.21f);
        private static readonly Color BrightGold = new Color(1f, 0.86f, 0.38f);

        private NameplateBarView _bar;
        private HashSet<NameplateSeatDirection> _occupiedSeats;
        private Image _dimOverlay;
        private TMP_Text _handCountText;
        private GameObject _rankBadge;
        private TMP_Text _rankText;

        // 強調の表現に使う部品（デザインによって使うものだけが入る）。
        private RectTransform _liftTarget;
        private RectTransform _cardShadow;
        private Image _cardShadowImage;
        private Image _glow;
        private RectTransform _chip;
        private Outline _goldOutline;

        private float _emphasis;
        private float _emphasisTarget;

        public int SeatIndex { get; private set; }

        /// <param name="occupiedSeats">ミニテーブル図に丸を出す座席（参加しているプレイヤーの、画面上の位置）。</param>
        public void Build(NameplateBarView bar, NameplateInfo info, HashSet<NameplateSeatDirection> occupiedSeats)
        {
            _bar = bar;
            _occupiedSeats = occupiedSeats;
            SeatIndex = info.SeatIndex;

            switch (bar.CurrentDesign)
            {
                case NameplateBarView.Design.B_CasinoChip:
                    BuildCasinoChip(info);
                    break;
                case NameplateBarView.Design.C_DealerPlate:
                    BuildDealerPlate(info);
                    break;
                default:
                    BuildPlayingCard(info);
                    break;
            }

            _dimOverlay = CreateImage("DimOverlay", transform, _bar.RoundedSprite, new Color(0f, 0f, 0f, _bar.FinishedDim));
            Stretch(_dimOverlay.rectTransform, 0f, 0f, 0f, 0f);
            _dimOverlay.gameObject.SetActive(false);

            BuildRankBadge();
            ApplyEmphasis();
        }

        /// <summary>手番の強調のオン・オフ。immediate なら補間せずに即座に反映する。</summary>
        public void SetHighlighted(bool highlighted, bool immediate = false)
        {
            _emphasisTarget = highlighted ? 1f : 0f;
            if (immediate)
            {
                _emphasis = _emphasisTarget;
                ApplyEmphasis();
            }
        }

        /// <summary>手札の残り枚数と順位（上がっていなければ null）を反映する。</summary>
        public void SetState(int handCount, int? rank)
        {
            if (_handCountText != null)
            {
                _handCountText.text = handCount > 0 ? $"残り{handCount}枚" : "あがり";
            }

            bool showRank = _bar.ShowRank && rank.HasValue;
            if (_rankBadge != null)
            {
                _rankBadge.SetActive(showRank);
                _rankText.text = rank.HasValue ? $"{rank.Value}位" : string.Empty;
            }
            _dimOverlay.gameObject.SetActive(showRank);
        }

        private void Update()
        {
            if (Mathf.Approximately(_emphasis, _emphasisTarget))
            {
                return;
            }

            float step = Time.unscaledDeltaTime / Mathf.Max(0.01f, _bar.HighlightDuration);
            _emphasis = Mathf.MoveTowards(_emphasis, _emphasisTarget, step);
            ApplyEmphasis();
        }

        private void ApplyEmphasis()
        {
            float e = Mathf.SmoothStep(0f, 1f, _emphasis);
            float strength = _bar.HighlightStrength;
            transform.localScale = Vector3.one * Mathf.Lerp(1f, _bar.HighlightScale, e);

            // 案A: カードが少し持ち上がり、影が伸びる。
            if (_liftTarget != null)
            {
                _liftTarget.anchoredPosition = new Vector2(0f, 6f * strength * e);
            }
            if (_cardShadow != null)
            {
                _cardShadow.anchoredPosition = new Vector2(3f + 3f * strength * e, -3f - 7f * strength * e);
                _cardShadowImage.color = new Color(0f, 0f, 0f, 0.28f + 0.17f * strength * e);
            }

            // 案B: チップの後ろが光り、チップが少し大きくなる。案C: 金の縁の外側がほのかに光る。
            if (_glow != null)
            {
                var c = _glow.color;
                _glow.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(0.75f * strength * e));
            }
            if (_chip != null)
            {
                _chip.localScale = Vector3.one * (1f + 0.06f * strength * e);
            }

            // 案C: 金の縁が太く、明るくなる。
            if (_goldOutline != null)
            {
                float width = 1.5f + 2.5f * strength * e;
                _goldOutline.effectDistance = new Vector2(width, -width);
                _goldOutline.effectColor = Color.Lerp(Gold, BrightGold, e);
            }
        }

        // ------------------------------------------------------------------
        // 案A: トランプ風
        // ------------------------------------------------------------------

        private void BuildPlayingCard(NameplateInfo info)
        {
            int suitIndex = (info.OrderNumber - 1) % Suits.Length;
            Color suitColor = suitIndex == 1 || suitIndex == 2 ? _bar.SuitRedColor : _bar.SuitBlackColor;

            _cardShadowImage = CreateImage("Shadow", transform, _bar.RoundedSprite, new Color(0f, 0f, 0f, 0.28f));
            _cardShadow = _cardShadowImage.rectTransform;
            Stretch(_cardShadow, 0f, 0f, 0f, 0f);

            var card = new GameObject("Card", typeof(RectTransform)).GetComponent<RectTransform>();
            card.SetParent(transform, false);
            Stretch(card, 0f, 0f, 0f, 0f);
            _liftTarget = card;

            var body = CreateImage("Body", card, _bar.RoundedSprite, new Color(1f, 0.98f, 0.92f));
            Stretch(body.rectTransform, 0f, 0f, 0f, 0f);
            var edge = body.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color(0.55f, 0.52f, 0.46f, 0.9f);
            edge.effectDistance = new Vector2(1f, -1f);

            // 左上の隅に、カードのインデックスのように「1P」とスートを縦に並べる。
            var order = CreateText("Order", card, _bar.TextFont, $"{info.OrderNumber}P", 20f, suitColor, TextAlignmentOptions.Center);
            order.fontStyle = FontStyles.Bold;
            PlaceLeftColumn(order.rectTransform, 6f, 48f, 0.52f, 0.95f);
            var suit = CreateText("Suit", card, _bar.SymbolFont, Suits[suitIndex], 32f, suitColor, TextAlignmentOptions.Center);
            PlaceLeftColumn(suit.rectTransform, 6f, 48f, 0.08f, 0.56f);

            BuildNameArea(card, info, 54f, TextAlignmentOptions.Center,
                new Color(0.10f, 0.10f, 0.12f), new Color(0.38f, 0.36f, 0.33f), suitColor);
        }

        // ------------------------------------------------------------------
        // 案B: カジノチップ風
        // ------------------------------------------------------------------

        private void BuildCasinoChip(NameplateInfo info)
        {
            float h = _bar.PlateSize.y;
            Color chipColor = ChipColors[(info.OrderNumber - 1) % ChipColors.Length];

            var band = CreateImage("Band", transform, _bar.RoundedSprite, new Color(0.08f, 0.10f, 0.11f, 1f));
            Stretch(band.rectTransform, h * 0.5f, 0f, 0.12f * h, 0.12f * h);
            var bandEdge = band.gameObject.AddComponent<Outline>();
            bandEdge.effectColor = new Color(1f, 1f, 1f, 0.25f);
            bandEdge.effectDistance = new Vector2(1f, -1f);

            _glow = CreateImage("Glow", transform, _bar.CircleSprite, new Color(1f, 0.85f, 0.35f, 0f));
            PlaceSquareAtLeft(_glow.rectTransform, h * 1.35f, h * 0.5f);

            _chip = new GameObject("Chip", typeof(RectTransform)).GetComponent<RectTransform>();
            _chip.SetParent(transform, false);
            PlaceSquareAtLeft(_chip, h, h * 0.5f);

            var disc = CreateImage("Disc", _chip, _bar.CircleSprite, chipColor);
            Stretch(disc.rectTransform, 0f, 0f, 0f, 0f);

            // 縁に等間隔の白い切れ込み（8か所）。
            for (int i = 0; i < 8; i++)
            {
                var notch = CreateImage($"Notch_{i}", _chip, null, new Color(1f, 1f, 1f, 0.95f));
                var r = notch.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                r.sizeDelta = new Vector2(h * 0.11f, h * 0.20f);
                float angle = i * 45f;
                r.localRotation = Quaternion.Euler(0f, 0f, -angle);
                r.anchoredPosition = Quaternion.Euler(0f, 0f, -angle) * new Vector3(0f, h * 0.39f, 0f);
            }

            var inner = CreateImage("Inner", _chip, _bar.CircleSprite, Color.Lerp(chipColor, Color.black, 0.15f));
            var innerRect = inner.rectTransform;
            innerRect.anchorMin = innerRect.anchorMax = new Vector2(0.5f, 0.5f);
            innerRect.sizeDelta = Vector2.one * h * 0.62f;
            var innerEdge = inner.gameObject.AddComponent<Outline>();
            innerEdge.effectColor = new Color(1f, 1f, 1f, 0.9f);
            innerEdge.effectDistance = new Vector2(1.5f, -1.5f);

            var order = CreateText("Order", _chip, _bar.TextFont, $"{info.OrderNumber}P", 22f, Color.white, TextAlignmentOptions.Center);
            order.fontStyle = FontStyles.Bold;
            Stretch(order.rectTransform, 0f, 0f, 0f, 0f);

            BuildNameArea(transform, info, h + 8f, TextAlignmentOptions.MidlineLeft,
                Color.white, new Color(0.78f, 0.80f, 0.80f), chipColor);
        }

        // ------------------------------------------------------------------
        // 案C: ディーラープレート風
        // ------------------------------------------------------------------

        private void BuildDealerPlate(NameplateInfo info)
        {
            _glow = CreateImage("Glow", transform, _bar.RoundedSprite, new Color(1f, 0.82f, 0.30f, 0f));
            Stretch(_glow.rectTransform, -7f, -7f, -7f, -7f);

            // 不透明にする（リニア色空間では少しの透過でも下の金の縁取りや空が透けて茶色っぽく見えるため）。
            var body = CreateImage("Body", transform, _bar.RoundedSprite, new Color(0.05f, 0.05f, 0.06f, 1f));
            Stretch(body.rectTransform, 0f, 0f, 0f, 0f);
            _goldOutline = body.gameObject.AddComponent<Outline>();
            _goldOutline.effectColor = Gold;
            _goldOutline.effectDistance = new Vector2(1.5f, -1.5f);

            var order = CreateText("Order", transform, _bar.TextFont, $"{info.OrderNumber}P", 26f, Gold, TextAlignmentOptions.Center);
            order.fontStyle = FontStyles.Bold;
            PlaceLeftColumn(order.rectTransform, 6f, 58f, 0f, 1f);

            var divider = CreateImage("Divider", transform, null, new Color(Gold.r, Gold.g, Gold.b, 0.8f));
            var d = divider.rectTransform;
            d.anchorMin = new Vector2(0f, 0.2f);
            d.anchorMax = new Vector2(0f, 0.8f);
            d.pivot = new Vector2(0.5f, 0.5f);
            d.sizeDelta = new Vector2(1.5f, 0f);
            d.anchoredPosition = new Vector2(62f, 0f);

            BuildNameArea(transform, info, 72f, TextAlignmentOptions.MidlineLeft,
                Color.white, new Color(0.75f, 0.75f, 0.75f), Gold);
        }

        // ------------------------------------------------------------------
        // 共通部品
        // ------------------------------------------------------------------

        /// <summary>
        /// 名前・（人間なら）「あなた」・（任意）残り枚数を左端leftからミニテーブル図の手前までに置き、
        /// 右端にミニテーブル図を置く。seatColor はミニテーブル図で自分の席を塗る色。
        /// </summary>
        private void BuildNameArea(Transform parent, NameplateInfo info, float left,
            TextAlignmentOptions nameAlignment, Color nameColor, Color subColor, Color seatColor)
        {
            float right = BuildMiniTable(parent, info.Direction, seatColor) + 6f;
            float h = _bar.PlateSize.y;

            // 人間のプレートだけ、名前の上に小さく「あなた」を出し、その分だけ名前を下げる。
            float nameTop = 6f;
            if (info.IsHuman)
            {
                var you = CreateText("You", parent, _bar.TextFont, "あなた", 15f, subColor, nameAlignment);
                Stretch(you.rectTransform, left, right, 5f, h - 22f);
                nameTop = 18f;
            }

            var name = CreateText("Name", parent, _bar.TextFont, info.DisplayName, 28f, nameColor, nameAlignment);
            name.enableAutoSizing = true;
            name.fontSizeMin = 16f;
            name.fontSizeMax = 28f;
            name.overflowMode = TextOverflowModes.Ellipsis;
            name.textWrappingMode = TextWrappingModes.NoWrap;

            if (_bar.ShowHandCount)
            {
                Stretch(name.rectTransform, left, right, nameTop + 2f, h * 0.42f);
                _handCountText = CreateText("HandCount", parent, _bar.TextFont, string.Empty, 17f, subColor, nameAlignment);
                Stretch(_handCountText.rectTransform, left, right, h * 0.58f, 6f);
            }
            else
            {
                Stretch(name.rectTransform, left, right, nameTop, 6f);
            }
        }

        /// <summary>
        /// テーブルを真上から見たミニテーブル図（角丸の四角＋4辺の中央の座席の丸）を、プレートの右端に縦中央で置く。
        /// 図の上下左右は通常のカメラから見た画面上の向き。mySeat の丸だけ seatColor で塗り、ほかの参加者の席は
        /// 輪郭だけ、参加していない席は <see cref="NameplateBarView.UnusedSeatAlpha"/> に応じて薄く表示するか表示しない。
        /// 戻り値は、プレートの右端から図の左端までの幅（名前の領域をその手前で止めるのに使う）。
        /// </summary>
        private float BuildMiniTable(Transform parent, NameplateSeatDirection mySeat, Color seatColor)
        {
            Vector2 tableSize = _bar.MiniTableSize;
            float dot = _bar.SeatDotSize;
            const float rightMargin = 8f;

            var root = new GameObject("MiniTable", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(parent, false);
            root.anchorMin = root.anchorMax = new Vector2(1f, 0.5f);
            root.pivot = new Vector2(1f, 0.5f);
            root.sizeDelta = tableSize + Vector2.one * dot;
            root.anchoredPosition = new Vector2(-rightMargin, 0f);

            var table = CreateImage("Table", root, _bar.RoundedSprite, _bar.MiniTableColor);
            // 9スライスの角（20px）を図の大きさに合わせて小さくする。
            table.pixelsPerUnitMultiplier = 4f;
            var tableRect = table.rectTransform;
            tableRect.anchorMin = tableRect.anchorMax = new Vector2(0.5f, 0.5f);
            tableRect.sizeDelta = tableSize;

            var seats = new[]
            {
                (Direction: NameplateSeatDirection.Down, Position: new Vector2(0f, -tableSize.y / 2f)),
                (Direction: NameplateSeatDirection.Left, Position: new Vector2(-tableSize.x / 2f, 0f)),
                (Direction: NameplateSeatDirection.Up, Position: new Vector2(0f, tableSize.y / 2f)),
                (Direction: NameplateSeatDirection.Right, Position: new Vector2(tableSize.x / 2f, 0f))
            };

            foreach (var seat in seats)
            {
                bool occupied = _occupiedSeats != null && _occupiedSeats.Contains(seat.Direction);
                if (!occupied && _bar.UnusedSeatAlpha <= 0f)
                {
                    continue;
                }

                bool mine = seat.Direction == mySeat;
                Color fill = mine ? seatColor : _bar.OtherSeatFillColor;
                Color outlineColor = mine ? new Color(1f, 1f, 1f, 0.9f) : _bar.OtherSeatOutlineColor;
                if (!occupied)
                {
                    fill.a *= _bar.UnusedSeatAlpha;
                    outlineColor.a *= _bar.UnusedSeatAlpha;
                }

                var circle = CreateImage($"Seat_{seat.Direction}", root, _bar.CircleSprite, fill);
                var r = circle.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                r.sizeDelta = Vector2.one * dot;
                r.anchoredPosition = seat.Position;
                var outline = circle.gameObject.AddComponent<Outline>();
                outline.effectColor = outlineColor;
                outline.effectDistance = new Vector2(1.2f, -1.2f);
            }

            return rightMargin + root.sizeDelta.x;
        }

        private void BuildRankBadge()
        {
            var badge = CreateImage("RankBadge", transform, _bar.RoundedSprite, new Color(0.96f, 0.80f, 0.28f));
            badge.pixelsPerUnitMultiplier = 2.5f;
            var r = badge.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(52f, 26f);
            r.anchoredPosition = new Vector2(-24f, -2f);
            var edge = badge.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color(0.35f, 0.25f, 0.05f, 0.9f);
            edge.effectDistance = new Vector2(1f, -1f);

            _rankText = CreateText("RankText", r, _bar.TextFont, string.Empty, 18f, new Color(0.15f, 0.10f, 0.02f), TextAlignmentOptions.Center);
            _rankText.fontStyle = FontStyles.Bold;
            Stretch(_rankText.rectTransform, 0f, 0f, 0f, 0f);

            _rankBadge = badge.gameObject;
            _rankBadge.SetActive(false);
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
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            return tmp;
        }

        /// <summary>親いっぱいに広げ、左・右・上・下の内側の余白を指定する（負値で外側にはみ出す）。</summary>
        private static void Stretch(RectTransform rect, float left, float right, float top, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>左端からxMin〜xMaxの幅で、高さ方向は親の割合yMin〜yMaxの範囲に置く。</summary>
        private static void PlaceLeftColumn(RectTransform rect, float xMin, float xMax, float yMin, float yMax)
        {
            rect.anchorMin = new Vector2(0f, yMin);
            rect.anchorMax = new Vector2(0f, yMax);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.offsetMin = new Vector2(xMin, 0f);
            rect.offsetMax = new Vector2(xMax, 0f);
        }

        /// <summary>左端から中心までcenterXの位置に、size四方の正方形を縦中央に置く。</summary>
        private static void PlaceSquareAtLeft(RectTransform rect, float size, float centerX)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.one * size;
            rect.anchoredPosition = new Vector2(centerX, 0f);
        }
    }
}
