using System;
using LuckyTrash.Cards;
using UnityEngine;

namespace LuckyTrash.UI
{
    /// <summary>
    /// テーブル上に実際に置かれた1枚の3Dカードを表すコンポーネント。
    /// 「Free Playing Cards Pack」の両面シェーダー（Reversibl_Draw、URP対応）マテリアルを
    /// そのまま活用する。各マテリアルは表面（スート・ランク別の絵柄）と裏面（共通の柄）を
    /// 1枚に持っているため、内容の切り替えはマテリアル差し替えで、表向き/裏向きの切り替えは
    /// このオブジェクト自身の回転（どちらの面をテーブル上向きにするか）で行う。
    /// メッシュは <see cref="ReferenceCardDrawView"/> と同じ "PlayingCards_00.fbx" の card
    /// サブメッシュを想定し、ローカルX(270,0,0)回転で寝かせた状態を「表向き」の基準姿勢とする。
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class Card3DView : MonoBehaviour
    {
        /// <summary>1スート分、ランク1(A)〜13(K)のマテリアルを順に並べたもの。</summary>
        [Serializable]
        public struct SuitFaceMaterials
        {
            [Tooltip("インデックス0=ランク1(A) 〜 インデックス12=ランク13(K)。")]
            public Material[] byRank;
        }

        /// <summary>テーブルに寝かせた状態で表向き（絵柄が上）になる基準姿勢。</summary>
        public static readonly Vector3 FaceUpEuler = new Vector3(0f, 0f, 0f);

        /// <summary>テーブルに寝かせた状態で裏向き（柄が上）になる姿勢（表向きから180度反転）。</summary>
        public static readonly Vector3 FaceDownEuler = new Vector3(180f, 0f, 0f);

        [SerializeField] private MeshRenderer _meshRenderer;

        [Header("Materials (Free Playing Cards Pack)")]
        [Tooltip("裏向き表示（中身を見せない）に使う、無地のブランクカードのマテリアル。")]
        [SerializeField] private Material _blankMaterial;
        [SerializeField] private SuitFaceMaterials _spadeFaces;
        [SerializeField] private SuitFaceMaterials _heartFaces;
        [SerializeField] private SuitFaceMaterials _diamondFaces;
        [SerializeField] private SuitFaceMaterials _clubFaces;

        private void Awake()
        {
            if (_meshRenderer == null)
            {
                _meshRenderer = GetComponent<MeshRenderer>();
            }
        }

        /// <summary>カードの内容を受け取り、表向きで表示する。</summary>
        public void SetCard(Card card)
        {
            ApplyMaterial(GetFaceMaterial(card.Suit, card.Rank));
            transform.localRotation = Quaternion.Euler(FaceUpEuler);
        }

        /// <summary>裏向き（中身が分からない状態）にする。CPUの手札表示に使う。</summary>
        public void SetFaceDown()
        {
            ApplyMaterial(_blankMaterial);
            transform.localRotation = Quaternion.Euler(FaceDownEuler);
        }

        private void ApplyMaterial(Material material)
        {
            if (_meshRenderer == null)
            {
                _meshRenderer = GetComponent<MeshRenderer>();
            }

            if (_meshRenderer != null && material != null)
            {
                _meshRenderer.sharedMaterial = material;
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
                Debug.LogWarning($"{nameof(Card3DView)}: {suit} {rank} のマテリアルが未設定です。", this);
                return null;
            }

            return set.byRank[rank - 1];
        }
    }
}
