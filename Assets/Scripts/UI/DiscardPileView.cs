using System.Collections;
using System.Collections.Generic;
using LuckyTrash.Cards;
using UnityEngine;

namespace LuckyTrash.UI
{
    /// <summary>
    /// 盤面の捨て札の山と、捨てられたカードがそこへ飛ぶ演出。
    /// 山は表向きのカードを1枚ごとに少しずつ回転・位置をずらして無造作に積んだ見た目にする。
    /// 表示するカードは上から <see cref="_maxVisibleCards"/> 枚までで、それより下は山の厚み（白っぽい板）で表す。
    /// 表示用のカードは最初に作っておいて使い回す。山の中身は GameState の捨て札（読み取りのみ）と同期する。
    /// 手札から捨てられるカードは、金色に光って浮き上がり（CPUの裏向きのカードは表向きにめくれ）、
    /// 放物線を描いてこの山へ飛んで着地する。山札が作り直されたときは、山のカードが山札の方へ移って消える。
    /// </summary>
    public class DiscardPileView : MonoBehaviour
    {
        [Header("カード")]
        [Tooltip("手札と同じカードのプレハブ（Card3DView）。山に表示するカードと、基準カードの写しに使う。")]
        [SerializeField] private Card3DView _cardPrefab;
        [Tooltip("山のカードの大きさ（山札・基準カードと同じ5倍）。")]
        [SerializeField] private float _cardScale = 5f;

        [Header("山の見た目")]
        [Tooltip("山の上から何枚までをカードとして表示するか（それより下は厚みで表す）。")]
        [SerializeField] private int _maxVisibleCards = 8;
        [Tooltip("1枚ごとの位置のずれの最大値（ワールド単位）。")]
        [SerializeField] private float _scatterRadius = 0.05f;
        [Tooltip("1枚ごとの回転のずれの最大値（度）。")]
        [SerializeField] private float _scatterAngle = 18f;
        [Tooltip("カード1枚ごとに積み上がる高さ。")]
        [SerializeField] private float _layerHeight = 0.0015f;
        [Tooltip("表示しないカード1枚あたりの山の厚み。")]
        [SerializeField] private float _hiddenThicknessPerCard = 0.0006f;
        [Tooltip("山の厚みの上限。")]
        [SerializeField] private float _maxThickness = 0.03f;
        [SerializeField] private Color _thicknessColor = new Color(0.93f, 0.91f, 0.86f);

        [Header("光る・浮き上がる")]
        [Tooltip("捨てられるカードの下に重ねる、金色の縁取りの板の色。")]
        [SerializeField] private Color _glowColor = new Color(1f, 0.80f, 0.25f, 0.9f);
        [Tooltip("金色の板の大きさ（カードに対する倍率）。")]
        [SerializeField] private float _glowScale = 1.18f;
        [Tooltip("光って浮き上がる時間（秒）。CPUの裏向きのカードはこの間に表向きにめくれる。")]
        [SerializeField] private float _glowSeconds = 0.2f;
        [Tooltip("浮き上がる高さ（ワールド単位）。")]
        [SerializeField] private float _liftHeight = 0.06f;

        [Header("飛ぶ")]
        [Tooltip("1枚が捨て札の山へ飛んで着地するまでの時間（秒）。")]
        [SerializeField] private float _flySeconds = 0.4f;
        [Tooltip("複数枚のとき、1枚ずつ飛び始めをずらす間隔（秒）。")]
        [SerializeField] private float _flyStagger = 0.08f;
        [Tooltip("放物線の高さ（ワールド単位）。")]
        [SerializeField] private float _flyArcHeight = 0.45f;

        [Header("ノートラッシュの震え")]
        [SerializeField] private float _shakeSeconds = 0.2f;
        [SerializeField] private float _shakeAmplitude = 0.025f;

        [Header("山札の作り直し")]
        [Tooltip("山札の位置。山札が作り直されたとき、山のカードがここへ移って消える。")]
        [SerializeField] private Transform _deckTarget;
        [Tooltip("山のカードが山札の方へ移って消える時間（秒）。")]
        [SerializeField] private float _reshuffleSeconds = 0.35f;

        // カードのメッシュ（PlayingCards_00 の card）の、スケール1での幅と長さ。
        private const float CardMeshWidth = 0.057f;
        private const float CardMeshLength = 0.0887f;

        private readonly List<Card3DView> _pool = new List<Card3DView>();
        private readonly List<Card> _visible = new List<Card>();
        private int _hiddenCount;
        private int _pendingLandings;
        private Transform _thickness;
        private Material _glowMaterial;

        private void Awake()
        {
            for (int i = 0; i < Mathf.Max(1, _maxVisibleCards); i++)
            {
                var card = Instantiate(_cardPrefab, transform);
                card.name = $"PileCard_{i}";
                card.gameObject.SetActive(false);
                _pool.Add(card);
            }

            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = "PileThickness";
            Destroy(block.GetComponent<Collider>());
            block.transform.SetParent(transform, false);
            var blockRenderer = block.GetComponent<MeshRenderer>();
            blockRenderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = _thicknessColor };
            _thickness = block.transform;
            _thickness.gameObject.SetActive(false);

            _glowMaterial = new Material(Shader.Find("Sprites/Default"));
        }

        /// <summary>
        /// 山の見た目を GameState の捨て札（下から順）に合わせる。excludeTop 枚は、まだ山に着いていない
        /// （演出で飛ばす前の）カードとして表示から除く。
        /// </summary>
        public void Sync(IReadOnlyList<Card> discardPile, int excludeTop = 0)
        {
            int total = Mathf.Max(0, discardPile.Count - excludeTop);
            int visibleCount = Mathf.Min(total, _pool.Count);
            _visible.Clear();
            for (int i = total - visibleCount; i < total; i++)
            {
                _visible.Add(discardPile[i]);
            }
            _hiddenCount = total - visibleCount;
            Refresh();
        }

        /// <summary>山の一番上（次に着地する位置）のワールド座標。</summary>
        public Vector3 TopPosition => transform.position + Vector3.up * (Thickness + _visible.Count * _layerHeight);

        /// <summary>
        /// 捨てられるカードを光らせて浮かせ（revealFaces なら表向きにめくり）、順番に捨て札の山へ飛ばすコルーチン。
        /// 基準カードの写し（reference のスロットの位置から。元のカードは写しが飛び立つときに隠す）も一緒に飛ばす。全部のカードが飛び立ったら onLaunched を呼ぶ
        /// （残った手札の並び直しに使う）。全部が着地したら終わる。
        /// </summary>
        public IEnumerator PlayDiscards(IReadOnlyList<Card3DView> handCards, IReadOnlyList<Card> cards, bool revealFaces,
            ReferenceCardDrawView reference, Card? referenceCard, System.Action onLaunched)
        {
            var glows = new List<Transform>();
            var startPositions = new List<Vector3>();
            var startRotations = new List<Quaternion>();
            for (int i = 0; i < handCards.Count; i++)
            {
                if (revealFaces)
                {
                    // 動かす前に、表のマテリアルを確定させる（白紙の面が見えないように）。
                    handCards[i].ShowFaceMaterial(cards[i]);
                }
                startPositions.Add(handCards[i].transform.position);
                startRotations.Add(handCards[i].transform.localRotation);
                glows.Add(CreateGlow(handCards[i].transform));
            }

            // 光って浮き上がる（裏向きのカードはこの間に表向きにめくれる）。
            float glowSeconds = Mathf.Max(0.01f, _glowSeconds);
            float elapsed = 0f;
            while (elapsed < glowSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / glowSeconds);
                float eased = 1f - (1f - t) * (1f - t);
                for (int i = 0; i < handCards.Count; i++)
                {
                    var card = handCards[i].transform;
                    card.position = startPositions[i] + Vector3.up * _liftHeight * eased;
                    if (revealFaces)
                    {
                        card.localRotation = Quaternion.Slerp(startRotations[i], Quaternion.Euler(Card3DView.FaceUpEuler), eased);
                    }
                    UpdateGlow(glows[i], card, t);
                }
                yield return null;
            }

            // 基準カードの写しを、スロットの位置・向きから飛ばす（基準カードもロジック上は捨て札になっている）。
            var flights = new List<Coroutine>();
            int launchIndex = 0;
            var referenceSource = reference != null && reference.CardRenderer != null ? reference.CardRenderer.transform : null;
            if (referenceSource != null && referenceSource.gameObject.activeInHierarchy && referenceCard.HasValue)
            {
                var copy = Instantiate(_cardPrefab, referenceSource.position, referenceSource.rotation);
                copy.SetCard(referenceCard.Value);
                copy.transform.rotation = referenceSource.rotation;
                copy.transform.localScale = referenceSource.lossyScale;
                reference.HideCard();
                flights.Add(StartCoroutine(Fly(copy.transform, referenceCard.Value, null, 0f)));
                launchIndex++;
            }

            for (int i = 0; i < handCards.Count; i++)
            {
                flights.Add(StartCoroutine(Fly(handCards[i].transform, cards[i], glows[i], launchIndex * _flyStagger)));
                launchIndex++;
            }

            // 全部が飛び立ったら、残った手札を並び直してよい。
            yield return new WaitForSeconds(Mathf.Max(0, launchIndex - 1) * _flyStagger);
            onLaunched?.Invoke();

            foreach (var flight in flights)
            {
                yield return flight;
            }
        }

        /// <summary>ノートラッシュのとき、手札全体を小さくぶるっと震わせる（カードは元の位置に戻る）。</summary>
        public IEnumerator Shake(IReadOnlyList<Card3DView> cards)
        {
            var origins = new List<Vector3>();
            foreach (var card in cards)
            {
                origins.Add(card.transform.localPosition);
            }

            float duration = Mathf.Max(0.01f, _shakeSeconds);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float offset = Mathf.Sin(t * Mathf.PI * 6f) * _shakeAmplitude * (1f - t);
                for (int i = 0; i < cards.Count; i++)
                {
                    if (cards[i] != null)
                    {
                        // 親（座席のアンカー）のスケールに関わらず、ワールドでほぼ同じ幅だけ揺らす。
                        float parentScale = cards[i].transform.parent != null ? cards[i].transform.parent.lossyScale.x : 1f;
                        cards[i].transform.localPosition = origins[i] + Vector3.right * offset / Mathf.Max(0.0001f, parentScale);
                    }
                }
                yield return null;
            }

            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] != null)
                {
                    cards[i].transform.localPosition = origins[i];
                }
            }
        }

        /// <summary>山札が捨て札から作り直されたとき、山のカードを山札の方へ移して消し、山を空にする。</summary>
        public void PlayReshuffle()
        {
            Vector3 deck = _deckTarget != null ? _deckTarget.position : transform.position;
            StartCoroutine(ReshuffleRoutine(deck));
        }

        private IEnumerator ReshuffleRoutine(Vector3 deckWorldPosition)
        {
            var moving = new List<Transform>();
            var starts = new List<Vector3>();
            foreach (var card in _pool)
            {
                if (card.gameObject.activeSelf)
                {
                    moving.Add(card.transform);
                    starts.Add(card.transform.position);
                }
            }
            if (_thickness.gameObject.activeSelf)
            {
                moving.Add(_thickness);
                starts.Add(_thickness.position);
            }
            var startScales = new List<Vector3>();
            foreach (var t in moving)
            {
                startScales.Add(t.localScale);
            }

            float duration = Mathf.Max(0.01f, _reshuffleSeconds);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = t * t;
                for (int i = 0; i < moving.Count; i++)
                {
                    moving[i].position = Vector3.Lerp(starts[i], deckWorldPosition, eased) + Vector3.up * 0.15f * Mathf.Sin(t * Mathf.PI);
                    moving[i].localScale = Vector3.Lerp(startScales[i], startScales[i] * 0.2f, eased);
                }
                yield return null;
            }

            _visible.Clear();
            _hiddenCount = 0;
            Refresh();
        }

        private IEnumerator Fly(Transform card, Card value, Transform glow, float delay)
        {
            if (delay > 0f)
            {
                float waited = 0f;
                while (waited < delay)
                {
                    waited += Time.deltaTime;
                    if (glow != null)
                    {
                        UpdateGlow(glow, card, 1f);
                    }
                    yield return null;
                }
            }

            // 着地する層を、飛び立つ時点で決めておく（同時に飛んでいるカードの分だけ上に積む）。
            int layer = Mathf.Min(_visible.Count + _pendingLandings, _pool.Count - 1);
            _pendingLandings++;
            GetPose(layer, value, out Vector3 endPosition, out Quaternion endRotation);

            card.SetParent(transform, true);
            Vector3 startPosition = card.position;
            Quaternion startRotation = card.rotation;
            Vector3 startScale = card.localScale;
            Vector3 endScale = Vector3.one * _cardScale;

            float duration = Mathf.Max(0.01f, _flySeconds);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, t);
                card.position = Vector3.Lerp(startPosition, endPosition, eased) + Vector3.up * _flyArcHeight * 4f * t * (1f - t);
                card.rotation = Quaternion.Slerp(startRotation, endRotation, eased);
                card.localScale = Vector3.Lerp(startScale, endScale, eased);
                if (glow != null)
                {
                    UpdateGlow(glow, card, 1f - t);
                }
                yield return null;
            }

            _pendingLandings--;
            if (glow != null)
            {
                Destroy(glow.gameObject);
            }
            Destroy(card.gameObject);
            AddToTop(value);
        }

        private void AddToTop(Card card)
        {
            _visible.Add(card);
            if (_visible.Count > _pool.Count)
            {
                _visible.RemoveAt(0);
                _hiddenCount++;
            }
            Refresh();
        }

        private float Thickness => Mathf.Min(_hiddenCount * _hiddenThicknessPerCard, _maxThickness);

        private void Refresh()
        {
            float thickness = Thickness;
            _thickness.gameObject.SetActive(thickness > 0.0001f);
            _thickness.localPosition = new Vector3(0f, thickness / 2f, 0f);
            _thickness.localRotation = Quaternion.identity;
            _thickness.localScale = new Vector3(CardMeshWidth * _cardScale, thickness, CardMeshLength * _cardScale);

            for (int i = 0; i < _pool.Count; i++)
            {
                var card = _pool[i];
                bool active = i < _visible.Count;
                card.gameObject.SetActive(active);
                if (!active)
                {
                    continue;
                }
                card.SetCard(_visible[i]);
                GetPose(i, _visible[i], out Vector3 position, out Quaternion rotation);
                card.transform.position = position;
                card.transform.rotation = rotation;
                card.transform.localScale = Vector3.one * _cardScale;
            }
        }

        /// <summary>
        /// 山の layer 番目（下から）に置くカードの位置と向き。ずれはカードごとに決まった乱数で、何度並べ直しても同じになる。
        /// </summary>
        private void GetPose(int layer, Card card, out Vector3 position, out Quaternion rotation)
        {
            var random = new System.Random(card.GetHashCode() * 31 + 7);
            float angle = (float)(random.NextDouble() * 2.0 - 1.0) * _scatterAngle;
            float dx = (float)(random.NextDouble() * 2.0 - 1.0) * _scatterRadius;
            float dz = (float)(random.NextDouble() * 2.0 - 1.0) * _scatterRadius;
            position = transform.position + new Vector3(dx, Thickness + (layer + 1) * _layerHeight, dz);
            rotation = transform.rotation * Quaternion.Euler(0f, angle, 0f) * Quaternion.Euler(Card3DView.FaceUpEuler);
        }

        /// <summary>カードの下に重ねる金色の板（Quad）を作る。カードの向きに関わらず、常にカードの真下に水平に置く。</summary>
        private Transform CreateGlow(Transform card)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "DiscardGlow";
            Destroy(quad.GetComponent<Collider>());
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.material = new Material(_glowMaterial) { color = new Color(_glowColor.r, _glowColor.g, _glowColor.b, 0f) };
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            UpdateGlow(quad.transform, card, 0f);
            return quad.transform;
        }

        private void UpdateGlow(Transform glow, Transform card, float strength)
        {
            float scale = card.lossyScale.x;
            glow.position = card.position - Vector3.up * 0.002f;
            Vector3 forward = Vector3.ProjectOnPlane(card.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }
            // Quad は XY 平面なので、X軸まわりに90度倒して水平にし、カードの長辺の向きにそろえる。
            glow.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);
            glow.localScale = new Vector3(CardMeshWidth * scale * _glowScale, CardMeshLength * scale * _glowScale, 1f);
            var material = glow.GetComponent<MeshRenderer>().material;
            material.color = new Color(_glowColor.r, _glowColor.g, _glowColor.b, _glowColor.a * Mathf.Clamp01(strength));
        }
    }
}
