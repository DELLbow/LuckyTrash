using System;
using System.Collections;
using LuckyTrash.Cards;
using UnityEngine;

namespace LuckyTrash.UI
{
    /// <summary>
    /// 「1枚引く」の演出（山札位置→基準カードスロットへ、裏向きのままスライド+フリップ移動する）を
    /// 担当するビュー。GameState.DrawReferenceCard() 自体の呼び出しタイミングには関与せず、
    /// 純粋に見た目のアニメーションだけを行う。
    ///
    /// 表示は「Free Playing Cards Pack」の3Dカードプレハブを、手札・ルーレットと同じメインの3D
    /// シーン内（テーブル上）に直接配置し、Main Cameraで直接撮影する方式（以前のDeckStage/
    /// RefCardStage + 専用カメラ + RenderTexture + RawImage という中継構成は廃止した）。
    /// カード裏表は、表/裏それぞれのテクスチャを1枚のマテリアルに持つ両面シェーダー
    /// （Reversibl_Draw、URP対応）に任せている。<b>引いたカードの正しいマテリアルは、
    /// アニメーション開始前（裏向きで山札から出てくる瞬間）に一度だけ設定し、以後は
    /// 差し替えない</b>（<see cref="PlayDrawAnimation"/> 参照）。両面シェーダーの性質上、
    /// 山札側を向いている間は自然に裏面（_BaseMapBack）が、フリップして基準カードスロットに
    /// 収まった時点では自然に表面（_BaseMap）が見える。
    ///
    /// 「山札から出てくる」ように見せるため、3Dの回転（<see cref="_cardSpinPivot"/> のX軸回転、
    /// テーブルに寝かせたまま表裏をひっくり返す <see cref="Card3DView"/> と同じ流儀）とは別に、
    /// このGameObject自身のワールド位置を、山札の3D位置（<see cref="_deckPosition"/>）から
    /// 本来の定位置（基準カードスロットの3D位置）までスライドさせる。
    /// </summary>
    public class ReferenceCardDrawView : MonoBehaviour
    {
        /// <summary>1スート分、ランク1(A)〜13(K)のマテリアルを順に並べたもの。</summary>
        [Serializable]
        private struct SuitFaceMaterials
        {
            [Tooltip("インデックス0=ランク1(A) 〜 インデックス12=ランク13(K)。")]
            public Material[] byRank;
        }

        [Header("3D Slide (このGameObject自身のTransform)")]
        [Tooltip("スライド開始位置＝山札の3D位置（このTransformのワールド座標をそのまま起点として使う）。")]
        [SerializeField] private Transform _deckPosition;

        [Header("3D Card Prop（フリップ回転のみ）")]
        [Tooltip("Y軸回転（裏↔表のフリップ）だけを行う対象。位置は動かさない。")]
        [SerializeField] private Transform _cardSpinPivot;
        [Tooltip("マテリアルを差し替える対象の MeshRenderer。")]
        [SerializeField] private MeshRenderer _cardMeshRenderer;

        [SerializeField] private float _drawDurationSeconds = 1.2f;

        [Header("Materials (Free Playing Cards Pack)")]
        [Tooltip("中身が未確定の間（ターン開始直後の待機状態）に使う、無地のブランクカードのマテリアル。")]
        [SerializeField] private Material _blankMaterial;
        [SerializeField] private SuitFaceMaterials _spadeFaces;
        [SerializeField] private SuitFaceMaterials _heartFaces;
        [SerializeField] private SuitFaceMaterials _diamondFaces;
        [SerializeField] private SuitFaceMaterials _clubFaces;

        private Vector3 _restingPosition;
        private bool _initialized;

        /// <summary>演出（スライド+フリップ）が進行中かどうか。</summary>
        public bool IsAnimating { get; private set; }

        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _restingPosition = transform.position;
            _initialized = true;
        }

        /// <summary>
        /// ターン開始時など、基準カードスロットを定位置・空のプレースホルダー状態に戻す。
        /// そのターンでまだカードが引かれていないことを表すため、非表示にする
        /// （<see cref="PlayDrawAnimation"/> 開始時に再び表示する）。
        /// </summary>
        public void ClearAndReset()
        {
            EnsureInitialized();

            transform.position = _restingPosition;

            if (_cardSpinPivot != null)
            {
                _cardSpinPivot.localRotation = Quaternion.Euler(Card3DView.FaceDownEuler);
            }

            SetMaterial(_blankMaterial);

            gameObject.SetActive(false);
        }

        /// <summary>
        /// 山札の位置から定位置（基準カードスロット）まで、スライド+フリップしながら移動する
        /// コルーチン。<paramref name="card"/> には、実際に引いたカードを呼び出し前に確定させて
        /// 渡す（GameState.DrawReferenceCard() を先に呼んでおく想定）。正しいマテリアルを
        /// アニメーション開始前に一度だけ設定するため、山札から出てくる瞬間は自然に裏面が、
        /// フリップ完了時は自然に表面が見える。
        /// </summary>
        public IEnumerator PlayDrawAnimation(Card card)
        {
            EnsureInitialized();
            IsAnimating = true;

            // まだカードが引かれていない間は非表示にしてあるので、演出開始と同時に表示する。
            gameObject.SetActive(true);

            // 引いたカードの正しいマテリアルを、アニメーション開始前に一度だけ設定する。
            // 以後は差し替えない（両面シェーダーが回転に応じて裏/表を自動的に見せる）。
            SetMaterial(GetFaceMaterial(card.Suit, card.Rank));

            Vector3 fromPosition = _deckPosition != null ? _deckPosition.position : _restingPosition;

            transform.position = fromPosition;

            if (_cardSpinPivot != null)
            {
                _cardSpinPivot.localRotation = Quaternion.Euler(Card3DView.FaceDownEuler);
            }

            float elapsed = 0f;

            while (elapsed < _drawDurationSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _drawDurationSeconds);

                // 移動: ease-out（滑り込むように減速して定位置に到着する）。
                float moveT = 1f - Mathf.Pow(1f - t, 3f);
                transform.position = Vector3.LerpUnclamped(fromPosition, _restingPosition, moveT);

                // フリップ: X軸回転を180→0度に変化させ、テーブルに寝かせたまま裏向き→表向きへ
                // 実際に回転させる（Card3DViewのFaceDown/FaceUpと同じ姿勢）。
                if (_cardSpinPivot != null)
                {
                    float flipAngle = Mathf.Lerp(180f, 0f, t);
                    _cardSpinPivot.localRotation = Quaternion.Euler(flipAngle, 0f, 0f);
                }

                yield return null;
            }

            transform.position = _restingPosition;

            if (_cardSpinPivot != null)
            {
                _cardSpinPivot.localRotation = Quaternion.Euler(Card3DView.FaceUpEuler);
            }

            IsAnimating = false;
        }

        private void SetMaterial(Material material)
        {
            if (_cardMeshRenderer != null && material != null)
            {
                _cardMeshRenderer.sharedMaterial = material;
            }
        }

        private Material GetFaceMaterial(Suit suit, int rank)
        {
            SuitFaceMaterials set;
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
                Debug.LogWarning($"{nameof(ReferenceCardDrawView)}: {suit} {rank} のマテリアルが未設定です。", this);
                return null;
            }

            return set.byRank[rank - 1];
        }
    }
}
