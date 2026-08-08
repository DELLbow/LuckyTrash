using System.Collections;
using LuckyTrash.Cards;
using UnityEngine;

namespace LuckyTrash.UI
{
    /// <summary>
    /// 「1枚引く」の演出（山札位置→基準カードスロットへ、裏向きのままスライド+フリップ移動する）を
    /// 担当するビュー。GameState.DrawReferenceCard() 自体の呼び出しタイミングには関与せず、
    /// 純粋に見た目のアニメーションだけを行う（実際のカードの中身は、アニメーション完了後に
    /// <see cref="ShowCard"/> を呼んで確定させる想定）。
    /// </summary>
    public class ReferenceCardDrawView : MonoBehaviour
    {
        [SerializeField] private RectTransform _rectTransform;
        [SerializeField] private CardView _cardView;
        [SerializeField] private RectTransform _deckPosition;
        [SerializeField] private float _drawDurationSeconds = 1.2f;

        private Vector2 _restingAnchoredPosition;
        private bool _initialized;

        /// <summary>演出（スライド+フリップ）が進行中かどうか。</summary>
        public bool IsAnimating { get; private set; }

        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            if (_rectTransform == null)
            {
                _rectTransform = (RectTransform)transform;
            }

            _restingAnchoredPosition = _rectTransform.anchoredPosition;
            _initialized = true;
        }

        /// <summary>
        /// ターン開始時など、基準カードスロットを定位置・空のプレースホルダー状態に戻す。
        /// </summary>
        public void ClearAndReset()
        {
            EnsureInitialized();
            _rectTransform.anchoredPosition = _restingAnchoredPosition;
            _rectTransform.localScale = Vector3.one;

            if (_cardView != null)
            {
                _cardView.SetBlankFace();
            }
        }

        /// <summary>
        /// 山札の位置から定位置（基準カードスロット）まで、裏向きのままスライド+フリップしながら
        /// 移動するコルーチン。まだ実際のカードの中身は決まっていない前提で、
        /// フリップの中間地点で「裏→表（中身は空）」に切り替わる。
        /// </summary>
        public IEnumerator PlayDrawAnimation()
        {
            EnsureInitialized();
            IsAnimating = true;

            Vector2 fromPosition = _deckPosition != null
                ? _deckPosition.anchoredPosition
                : _restingAnchoredPosition;

            _rectTransform.anchoredPosition = fromPosition;
            _rectTransform.localScale = Vector3.one;

            if (_cardView != null)
            {
                _cardView.SetFaceDown();
            }

            bool flippedToFront = false;
            float elapsed = 0f;

            while (elapsed < _drawDurationSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _drawDurationSeconds);

                // 移動: ease-out（滑り込むように減速して定位置に到着する）。
                float moveT = 1f - Mathf.Pow(1f - t, 3f);
                _rectTransform.anchoredPosition = Vector2.LerpUnclamped(fromPosition, _restingAnchoredPosition, moveT);

                // フリップ: 横幅を 1→0→1 に変化させ、裏返るような見た目にする。
                float scaleX = Mathf.Abs(Mathf.Cos(Mathf.PI * t));
                _rectTransform.localScale = new Vector3(scaleX, 1f, 1f);

                if (!flippedToFront && t >= 0.5f)
                {
                    flippedToFront = true;
                    if (_cardView != null)
                    {
                        _cardView.SetBlankFace();
                    }
                }

                yield return null;
            }

            _rectTransform.anchoredPosition = _restingAnchoredPosition;
            _rectTransform.localScale = Vector3.one;
            IsAnimating = false;
        }

        /// <summary>
        /// アニメーション完了後、実際に引いたカードの中身を即座に表示する（追加演出なし）。
        /// </summary>
        public void ShowCard(Card card)
        {
            EnsureInitialized();
            if (_cardView != null)
            {
                _cardView.SetCard(card);
            }
        }
    }
}
