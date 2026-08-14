using System;
using System.Collections.Generic;
using LuckyTrash.Cards;
using TMPro;
using UnityEngine;

namespace LuckyTrash.UI
{
    /// <summary>
    /// 手札（IReadOnlyList&lt;Card&gt;）を受け取り、その内容に応じて <see cref="Card3DView"/> を
    /// 動的に生成・破棄して、テーブル上の3D空間に横一列に並べて表示するコンポーネント。
    /// 実際の3D配置先（座席ごとの位置・向き）は <see cref="_cardContainer3D"/>
    /// （テーブル上に置かれた、座席ごとの3Dアンカー Transform）に一任し、このコンポーネント自身は
    /// 生成したカードをその子として、ローカルX軸方向に等間隔で並べるだけ。座席の向き
    /// （下/右/上/左）は <see cref="_cardContainer3D"/> 側の回転で決まる。
    /// プレイヤー名ラベル（<see cref="SetPlayerLabel"/>）は現状通り2D UI（Canvas）のまま、
    /// このコンポーネント自身のGameObjectに付けて運用する。
    /// </summary>
    public class HandView : MonoBehaviour
    {
        [SerializeField] private Card3DView _cardViewPrefab;
        [Tooltip("カードを実際に並べる3D空間上のアンカー。テーブル上の座席位置・向きを表す。")]
        [SerializeField] private Transform _cardContainer3D;
        [Tooltip("この座席のプレイヤー名を表示するラベル（任意）。CPU対戦で「CPU 1」等を表示する。")]
        [SerializeField] private TMP_Text _nameLabel;

        [Header("Layout")]
        [Tooltip("カード間の中心間隔（ワールド単位）。")]
        [SerializeField] private float _cardSpacing = 0.32f;
        [Tooltip("カード1枚の表示スケール（ワールド単位の倍率）。座席ごとにカメラからの距離が異なるため、" +
            "個別に調整できるようにしている（人間の下座席は特に大きくして読みやすくする想定）。")]
        [SerializeField] private float _cardScale = 5f;

        private readonly List<Card3DView> _cardViews = new List<Card3DView>();

        private Transform CardContainer => _cardContainer3D != null ? _cardContainer3D : transform;

        /// <summary>
        /// 手札の内容に応じて表示を更新する。呼び出すたびに、現在渡された手札の枚数・内容に
        /// 合わせて Card3DView の生成・破棄・並び替えを行う。
        /// </summary>
        /// <param name="hand">表示する手札。</param>
        /// <param name="faceDown">
        /// true の場合、枚数はそのまま表示しつつ全カードを裏向き（<see cref="Card3DView.SetFaceDown"/>）
        /// にする。CPUの手札を表示する際に使う。既定は false（表向き）。
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

            // 枚数が変わっても正しく表示できるよう、必要な数だけ Card3DView を過不足なく用意する。
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

                _cardViews[i].transform.localPosition = GetSlotLocalPosition(i, hand.Count);
                _cardViews[i].transform.localScale = Vector3.one * _cardScale;
            }
        }

        /// <summary>
        /// 手札のi番目（0始まり）のカードを、count枚を横一列に並べたときのローカル座標
        /// （<see cref="_cardContainer3D"/> 基準、中央揃え）を計算する。
        /// </summary>
        private Vector3 GetSlotLocalPosition(int index, int count)
        {
            float offset = (index - (count - 1) / 2f) * _cardSpacing;
            return new Vector3(offset, 0f, 0f);
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
