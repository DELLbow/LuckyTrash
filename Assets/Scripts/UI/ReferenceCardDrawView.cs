using System;
using System.Collections;
using LuckyTrash.Cards;
using UnityEngine;

namespace LuckyTrash.UI
{
    /// <summary>
    /// 「1枚引く」の演出（山札の一番上のカードを、本のページのようにめくって基準カードスロットへ
    /// 表向きに置く）を担当するビュー。GameState.DrawReferenceCard() 自体の呼び出しタイミングには関与せず、
    /// 純粋に見た目のアニメーションだけを行う。
    ///
    /// 表示は「Free Playing Cards Pack」の3Dカードプレハブを、手札・ルーレットと同じメインの3D
    /// シーン内（テーブル上）に直接配置し、Main Cameraで直接撮影する方式（以前のDeckStage/
    /// RefCardStage + 専用カメラ + RenderTexture + RawImage という中継構成は廃止した）。
    /// カード裏表は、表/裏それぞれのテクスチャを1枚のマテリアルに持つ両面シェーダー
    /// （Reversibl_Draw、URP対応）に任せている。<b>引いたカードの正しいマテリアルは、
    /// アニメーション開始前（裏向きで山札から出てくる瞬間）に一度だけ設定し、以後は
    /// 差し替えない</b>（<see cref="PlayDrawAnimation"/> 参照）。両面シェーダーの性質上、
    /// 山札の上にある間は自然に裏面（_BaseMapBack）が、めくれて基準カードスロットに
    /// 収まった時点では自然に表面（_BaseMap）が見える。
    ///
    /// めくりは、山札の中心と基準カードスロットの中心のちょうど中間にある、2点を結ぶ方向と直交する
    /// 水平な直線を「本の背」（回転軸）にして、<see cref="_cardSpinPivot"/> のワールド姿勢を180度回す。
    /// 軸の高さは山札の上面から始まり、カードが基準カードスロット側へ倒れた後半（90度以降）だけ
    /// 少しずつ下げて、山札の厚みの分の高さの差を吸収する。カードは常に軸より上を通るため、
    /// フェルトにも山札にも潜らない。このGameObject自身（スロットの定位置）は動かさない。
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

        [Header("山札")]
        [Tooltip("山札の3D位置。めくりの回転軸（山札と基準カードスロットの中間）と、山札の上面の高さ" +
            "（この下にあるRendererの上端）を求めるのに使う。")]
        [SerializeField] private Transform _deckPosition;

        [Header("3D Card Prop")]
        [Tooltip("めくりで位置・回転を動かす対象（カードのメッシュの親）。演出の最後に、表向きの定位置へ戻す。")]
        [SerializeField] private Transform _cardSpinPivot;
        [Tooltip("マテリアルを差し替える対象の MeshRenderer。")]
        [SerializeField] private MeshRenderer _cardMeshRenderer;

        [Header("ページめくり")]
        [Tooltip("めくり（180度回転）にかける時間（秒）。予備動作の持ち上げは含まない。")]
        [SerializeField] private float _flipDurationSeconds = 0.7f;
        [Tooltip("めくりの進み方（横軸=時間0〜1、縦軸=めくり角度0〜1）。既定は、めくり始めはゆっくり、" +
            "倒れるときに少し加速し、着地でふわっと止まる。")]
        [SerializeField] private AnimationCurve _flipCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0f),
            new Keyframe(0.6f, 0.7f, 1.8f, 1.8f),
            new Keyframe(1f, 1f, 0f, 0f));
        [Tooltip("めくり始める前に、山札の上でカードを少し持ち上げる予備動作の時間（秒）。0で省略。")]
        [SerializeField] private float _preLiftSeconds = 0.1f;
        [Tooltip("予備動作で持ち上げる高さ（ワールド単位）。")]
        [SerializeField] private float _preLiftHeight = 0.015f;

        [Header("Materials (Free Playing Cards Pack)")]
        [Tooltip("中身が未確定の間（ターン開始直後の待機状態）に使う、無地のブランクカードのマテリアル。")]
        [SerializeField] private Material _blankMaterial;
        [SerializeField] private SuitFaceMaterials _spadeFaces;
        [SerializeField] private SuitFaceMaterials _heartFaces;
        [SerializeField] private SuitFaceMaterials _diamondFaces;
        [SerializeField] private SuitFaceMaterials _clubFaces;

        // 山札の上面とカードが重なってちらつかないよう、めくり開始時にわずかに浮かせる高さ。
        private const float DeckTopClearance = 0.001f;

        private Vector3 _restingPosition;
        private Vector3 _pivotRestLocalPosition;
        private bool _initialized;

        /// <summary>演出（ページめくり）が進行中かどうか。</summary>
        public bool IsAnimating { get; private set; }

        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _restingPosition = transform.position;
            _pivotRestLocalPosition = _cardSpinPivot != null ? _cardSpinPivot.localPosition : Vector3.zero;
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
                _cardSpinPivot.localPosition = _pivotRestLocalPosition;
                _cardSpinPivot.localRotation = Quaternion.Euler(Card3DView.FaceDownEuler);
            }

            SetMaterial(_blankMaterial);

            gameObject.SetActive(false);
        }

        /// <summary>
        /// 山札の一番上（裏向き）から、本のページをめくるように180度めくって、基準カードスロットに
        /// 表向きで置くコルーチン。<paramref name="card"/> には、実際に引いたカードを呼び出し前に確定させて
        /// 渡す（GameState.DrawReferenceCard() を先に呼んでおく想定）。正しいマテリアルを
        /// アニメーション開始前に一度だけ設定するため、山札の上では自然に裏面が、
        /// 着地後は自然に表面が見える。
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

            transform.position = _restingPosition;

            if (_cardSpinPivot != null && _deckPosition != null)
            {
                yield return PlayPageFlip();
            }

            // 着地の姿勢を、基準カードスロットの表向きの定位置に値でそろえて確定する。
            transform.position = _restingPosition;

            if (_cardSpinPivot != null)
            {
                _cardSpinPivot.localPosition = _pivotRestLocalPosition;
                _cardSpinPivot.localRotation = Quaternion.Euler(Card3DView.FaceUpEuler);
            }

            IsAnimating = false;
        }

        /// <summary>
        /// 回転軸（山札と基準カードスロットの中間、2点を結ぶ方向と直交する水平線）を中心に、
        /// 表向きの定位置の姿勢を基準として -180度 → 0度 まで回す。
        /// 角度θ(0〜180)のときの姿勢は「定位置の姿勢を、軸のまわりに(θ-180)度回したもの」なので、
        /// θ=180で定位置の姿勢と完全に一致する。
        /// </summary>
        private IEnumerator PlayPageFlip()
        {
            // 表向きの定位置での、ピボットのワールド姿勢（着地の目標）。
            _cardSpinPivot.localPosition = _pivotRestLocalPosition;
            _cardSpinPivot.localRotation = Quaternion.Euler(Card3DView.FaceUpEuler);
            Vector3 restPosition = _cardSpinPivot.position;
            Quaternion restRotation = _cardSpinPivot.rotation;

            Vector3 deckToSlot = restPosition - _deckPosition.position;
            deckToSlot.y = 0f;
            if (deckToSlot.sqrMagnitude < 0.0001f)
            {
                yield break;
            }

            // 回転軸の水平位置（山札と基準カードスロットの中心の中間）と、軸から定位置への水平ベクトル。
            Vector3 axisPoint = restPosition - deckToSlot * 0.5f;
            Vector3 axisToRest = restPosition - new Vector3(axisPoint.x, restPosition.y, axisPoint.z);

            // 2点を結ぶ方向と直交する水平な軸。めくっている途中(90度)でカードが軸より上に来る向きを選ぶ
            // （＝山札側から立ち上がり、基準カードスロット側＝ルーレット側へ倒れる）。
            Vector3 axisDirection = Vector3.Cross(Vector3.up, deckToSlot.normalized);
            if ((Quaternion.AngleAxis(-90f, axisDirection) * axisToRest).y < 0f)
            {
                axisDirection = -axisDirection;
            }

            float deckTopY = GetDeckTopY() + DeckTopClearance;
            float restY = restPosition.y;
            float lift = Mathf.Max(0f, _preLiftHeight);

            // 予備動作: 山札の上（裏向き）で、カードを少しだけ持ち上げる。
            float preLiftSeconds = Mathf.Max(0f, _preLiftSeconds);
            float elapsed = 0f;
            while (elapsed < preLiftSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / preLiftSeconds);
                ApplyFlipPose(0f, deckTopY + lift * Mathf.SmoothStep(0f, 1f, t),
                    axisPoint, axisDirection, axisToRest, restRotation);
                yield return null;
            }

            // めくり本体。軸の高さは、カードが基準カードスロット側へ倒れた後半(90度以降)だけ下げる。
            // 前半から下げると、山札の上にあるカードの手前側が山札にめり込むため。
            float startAxisY = deckTopY + lift;
            float duration = Mathf.Max(0.01f, _flipDurationSeconds);
            elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float angle = Mathf.Clamp01(_flipCurve.Evaluate(t)) * 180f;
                float drop = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(90f, 180f, angle));
                float axisY = Mathf.Lerp(startAxisY, restY, drop);
                ApplyFlipPose(angle, axisY, axisPoint, axisDirection, axisToRest, restRotation);
                yield return null;
            }
        }

        /// <summary>
        /// めくり角度angle(0=山札の上で裏向き、180=基準カードスロットで表向き)、軸の高さaxisYのときの
        /// ピボットのワールド姿勢を設定する。
        /// </summary>
        private void ApplyFlipPose(float angle, float axisY, Vector3 axisPoint, Vector3 axisDirection,
            Vector3 axisToRest, Quaternion restRotation)
        {
            Quaternion swing = Quaternion.AngleAxis(angle - 180f, axisDirection);
            _cardSpinPivot.position = new Vector3(axisPoint.x, axisY, axisPoint.z) + swing * axisToRest;
            _cardSpinPivot.rotation = swing * restRotation;
        }

        /// <summary>
        /// 山札の上面（山札のTransform以下にあるRendererの上端）のワールドY座標。
        /// Rendererが無い場合は山札のTransformの高さを使う。
        /// </summary>
        private float GetDeckTopY()
        {
            float top = _deckPosition.position.y;
            foreach (var deckRenderer in _deckPosition.GetComponentsInChildren<Renderer>())
            {
                top = Mathf.Max(top, deckRenderer.bounds.max.y);
            }
            return top;
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
