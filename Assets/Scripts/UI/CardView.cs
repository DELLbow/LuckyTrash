using LuckyTrash.Cards;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LuckyTrash.UI
{
    /// <summary>
    /// 1枚のカードを表すUIコンポーネント（テキストベースのプレースホルダー表示）。
    /// 白背景のImageの上に「マークの記号+数字」（例: ♥7, ♠J）を表示し、
    /// Card.Color に応じて文字色を赤/黒に切り替える。
    /// プレハブ化して HandView から動的に生成する想定。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class CardView : MonoBehaviour
    {
        // TMPの標準的な赤/黒よりも見やすいよう、少し落ち着かせた色を既定値にしている。
        private static readonly Color RedTextColor = new Color(0.80f, 0.05f, 0.05f);
        private static readonly Color BlackTextColor = Color.black;

        // カード裏面のプレースホルダー色（新規アート素材は用意せず、背景色の切り替えで表現する）。
        private static readonly Color BackColor = new Color(0.15f, 0.25f, 0.55f);
        private static readonly Color FrontColor = Color.white;

        [SerializeField] private Image _background;
        [SerializeField] private TMP_Text _label;

        /// <summary>
        /// カードの内容を受け取り、表示（背景・テキスト・文字色）を更新する（表向き）。
        /// </summary>
        public void SetCard(Card card)
        {
            if (_background != null)
            {
                _background.color = FrontColor;
            }

            if (_label != null)
            {
                _label.text = FormatCardText(card);
                _label.color = card.Color == CardColor.Red ? RedTextColor : BlackTextColor;
            }
        }

        /// <summary>
        /// カードを裏向き（中身が分からない状態）にする。基準カードのドロー演出などで、
        /// まだ実際に引いたカードが確定していない間の表示に使う。
        /// </summary>
        public void SetFaceDown()
        {
            if (_background != null)
            {
                _background.color = BackColor;
            }

            if (_label != null)
            {
                _label.text = string.Empty;
            }
        }

        /// <summary>
        /// 表向きだが中身が空のプレースホルダー状態にする（基準カードスロットの待機時や、
        /// ドロー演出のフリップ後〜実際の中身確定までの一瞬に使う）。
        /// </summary>
        public void SetBlankFace()
        {
            if (_background != null)
            {
                _background.color = FrontColor;
            }

            if (_label != null)
            {
                _label.text = string.Empty;
            }
        }

        /// <summary>
        /// 「マークの記号+数字」形式のテキストを生成する（例: ♥7, ♠J）。
        /// GameController のステータス表示など、CardView 以外からの再利用も想定して公開している。
        /// </summary>
        public static string FormatCardText(Card card)
        {
            return FormatSuitSymbol(card.Suit) + FormatRankText(card.Rank);
        }

        private static string FormatSuitSymbol(Suit suit)
        {
            switch (suit)
            {
                case Suit.Spade:
                    return "♠";
                case Suit.Heart:
                    return "♥";
                case Suit.Diamond:
                    return "♦";
                case Suit.Club:
                    return "♣";
                default:
                    return "?";
            }
        }

        private static string FormatRankText(int rank)
        {
            switch (rank)
            {
                case 1:
                    return "A";
                case 11:
                    return "J";
                case 12:
                    return "Q";
                case 13:
                    return "K";
                default:
                    return rank.ToString();
            }
        }
    }
}
