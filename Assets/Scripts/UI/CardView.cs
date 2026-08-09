using System;
using LuckyTrash.Cards;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LuckyTrash.UI
{
    /// <summary>
    /// 1枚のカードを表すUIコンポーネント。
    /// 表向き表示は「Free Playing Cards Pack」(Game Asset Studio) のスプライトを使う。
    /// Card(Suit, Rank) からスート別×ランク別の配列を引いて対応するスプライトを表示し、
    /// 裏向き表示にはアセットのカード裏面スプライトを使う（テキストによるランク+スート表記は
    /// アートに含まれているため、既存の <see cref="_label"/> は空文字にして非表示にする）。
    /// プレハブ化して HandView から動的に生成する想定。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class CardView : MonoBehaviour
    {
        /// <summary>1スート分、ランク1(A)〜13(K)のスプライトを順に並べたもの。</summary>
        [Serializable]
        private struct SuitFaceSprites
        {
            [Tooltip("インデックス0=ランク1(A) 〜 インデックス12=ランク13(K)。")]
            public Sprite[] byRank;
        }

        [Header("Face Sprites (Free Playing Cards Pack)")]
        [SerializeField] private SuitFaceSprites _spadeFaces;
        [SerializeField] private SuitFaceSprites _heartFaces;
        [SerializeField] private SuitFaceSprites _diamondFaces;
        [SerializeField] private SuitFaceSprites _clubFaces;

        [Header("Back Sprite")]
        [SerializeField] private Sprite _backSprite;

        [SerializeField] private Image _background;
        [SerializeField] private TMP_Text _label;

        /// <summary>
        /// カードの内容を受け取り、表示（背景スプライト）を更新する（表向き）。
        /// </summary>
        public void SetCard(Card card)
        {
            ApplySprite(GetFaceSprite(card.Suit, card.Rank));
        }

        /// <summary>
        /// カードを裏向き（中身が分からない状態）にする。基準カードのドロー演出などで、
        /// まだ実際に引いたカードが確定していない間の表示に使う。
        /// </summary>
        public void SetFaceDown()
        {
            ApplySprite(_backSprite);
        }

        /// <summary>
        /// 表向きだが中身が空のプレースホルダー状態にする（基準カードスロットの待機時や、
        /// ドロー演出のフリップ後〜実際の中身確定までの一瞬に使う）。対応する絵柄が無い状態
        /// なので、無地の白背景（スプライト無し）で表す。
        /// </summary>
        public void SetBlankFace()
        {
            ApplySprite(null);
        }

        private void ApplySprite(Sprite sprite)
        {
            if (_background != null)
            {
                _background.sprite = sprite;
                _background.color = Color.white;
                _background.preserveAspect = true;
            }

            // ランク+スートはスプライトの絵柄で判別できるため、旧テキスト表記は表示しない。
            if (_label != null)
            {
                _label.text = string.Empty;
            }
        }

        private Sprite GetFaceSprite(Suit suit, int rank)
        {
            SuitFaceSprites set;
            switch (suit)
            {
                case Suit.Spade:
                    set = _spadeFaces;
                    break;
                case Suit.Heart:
                    set = _heartFaces;
                    break;
                case Suit.Diamond:
                    set = _diamondFaces;
                    break;
                case Suit.Club:
                    set = _clubFaces;
                    break;
                default:
                    return null;
            }

            if (set.byRank == null || rank < 1 || rank > set.byRank.Length)
            {
                Debug.LogWarning($"{nameof(CardView)}: {suit} {rank} のスプライトが未設定です。", this);
                return null;
            }

            return set.byRank[rank - 1];
        }

        /// <summary>
        /// 「マークの記号+数字」形式のテキストを生成する（例: ♥7, ♠J）。
        /// GameController のデバッグ表示など、CardView 以外からの再利用も想定して公開している。
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
