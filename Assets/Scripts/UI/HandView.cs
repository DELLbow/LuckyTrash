using System;
using System.Collections.Generic;
using LuckyTrash.Cards;
using TMPro;
using UnityEngine;

namespace LuckyTrash.UI
{
    /// <summary>
    /// 手札（IReadOnlyList&lt;Card&gt;）を受け取り、その内容に応じて CardView を動的に
    /// 生成・破棄して表示するコンポーネント。横一列の実際の並びは、このコンポーネントと
    /// 同じ GameObject（または cardContainer に指定した RectTransform）に付いている
    /// HorizontalLayoutGroup 等、Unity標準のレイアウト機能に任せる。
    /// この座席のプレイヤー名ラベル（<see cref="SetPlayerLabel"/>）も合わせて管理する。
    /// </summary>
    public class HandView : MonoBehaviour
    {
        [SerializeField] private CardView _cardViewPrefab;
        [SerializeField] private RectTransform _cardContainer;
        [Tooltip("この座席のプレイヤー名を表示するラベル（任意）。CPU対戦で「CPU 1」等を表示する。")]
        [SerializeField] private TMP_Text _nameLabel;

        private readonly List<CardView> _cardViews = new List<CardView>();

        private RectTransform CardContainer => _cardContainer != null ? _cardContainer : (RectTransform)transform;

        /// <summary>
        /// 手札の内容に応じて表示を更新する。呼び出すたびに、現在渡された手札の枚数・内容に
        /// 合わせて CardView の生成・破棄・並び替えを行う。
        /// </summary>
        /// <param name="hand">表示する手札。</param>
        /// <param name="faceDown">
        /// true の場合、枚数はそのまま表示しつつ全カードを裏向き（<see cref="CardView.SetFaceDown"/>）にする。
        /// CPUの手札を表示する際に使う。既定は false（表向き）。
        /// </param>
        public void SetHand(IReadOnlyList<Card> hand, bool faceDown = false)
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
                if (faceDown)
                {
                    _cardViews[i].SetFaceDown();
                }
                else
                {
                    _cardViews[i].SetCard(hand[i]);
                }

                _cardViews[i].transform.SetSiblingIndex(i);
            }
        }

        /// <summary>
        /// この座席のプレイヤー名ラベルを更新する（人間はTitleSceneの保存名、CPUは「CPU 1」等）。
        /// </summary>
        public void SetPlayerLabel(string displayName)
        {
            if (_nameLabel != null)
            {
                _nameLabel.text = displayName;
            }
        }
    }
}
