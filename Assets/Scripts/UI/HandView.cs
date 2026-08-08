using System;
using System.Collections.Generic;
using LuckyTrash.Cards;
using UnityEngine;

namespace LuckyTrash.UI
{
    /// <summary>
    /// 手札（IReadOnlyList&lt;Card&gt;）を受け取り、その内容に応じて CardView を動的に
    /// 生成・破棄して表示するコンポーネント。横一列の実際の並びは、このコンポーネントと
    /// 同じ GameObject（または cardContainer に指定した RectTransform）に付いている
    /// HorizontalLayoutGroup 等、Unity標準のレイアウト機能に任せる。
    /// </summary>
    public class HandView : MonoBehaviour
    {
        [SerializeField] private CardView _cardViewPrefab;
        [SerializeField] private RectTransform _cardContainer;

        private readonly List<CardView> _cardViews = new List<CardView>();

        private RectTransform CardContainer => _cardContainer != null ? _cardContainer : (RectTransform)transform;

        /// <summary>
        /// 手札の内容に応じて表示を更新する。呼び出すたびに、現在渡された手札の枚数・内容に
        /// 合わせて CardView の生成・破棄・並び替えを行う。
        /// </summary>
        public void SetHand(IReadOnlyList<Card> hand)
        {
            if (hand == null)
            {
                hand = Array.Empty<Card>();
            }

            if (_cardViewPrefab == null)
            {
                Debug.LogError($"{nameof(HandView)}: {nameof(_cardViewPrefab)} が設定されていません。", this);
                return;
            }

            // 枚数が変わっても正しく表示できるよう、必要な数だけ CardView を過不足なく用意する。
            while (_cardViews.Count < hand.Count)
            {
                var cardView = Instantiate(_cardViewPrefab, CardContainer);
                _cardViews.Add(cardView);
            }

            while (_cardViews.Count > hand.Count)
            {
                int lastIndex = _cardViews.Count - 1;
                var cardView = _cardViews[lastIndex];
                _cardViews.RemoveAt(lastIndex);
                if (cardView != null)
                {
                    Destroy(cardView.gameObject);
                }
            }

            for (int i = 0; i < hand.Count; i++)
            {
                _cardViews[i].SetCard(hand[i]);
                _cardViews[i].transform.SetSiblingIndex(i);
            }
        }
    }
}
