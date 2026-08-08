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

        [SerializeField] private Image _background;
        [SerializeField] private TMP_Text _label;

        /// <summary>
        /// カードの内容を受け取り、表示（テキストと文字色）を更新する。
        /// </summary>
        public void SetCard(Card card)
        {
            if (_label != null)
            {
                _label.text = FormatCardText(card);
                _label.color = card.Color == CardColor.Red ? RedTextColor : BlackTextColor;
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
