using UnityEngine;

namespace LuckyTrash.UI
{
    /// <summary>
    /// 手番の人の手札の下のフェルトを、ぼんやり明るい楕円で照らす演出（任意）。
    /// 座席ごとに、フェルトのすぐ上に半透明の楕円の平面（中心ほど明るく、外側へ透明になる頂点カラー）を置き、
    /// 手番の座席の楕円だけをふわっと表示する。ライトやポストプロセスを使わないため、スマホでも軽い。
    /// カードはこの平面より上にあるため、照らされるのはカードの周りのフェルトだけになる。
    /// </summary>
    public class TurnSpotlightView : MonoBehaviour
    {
        [Header("座席（CameraDirector.SeatSide の順: Bottom, Right, Top, Left）")]
        [SerializeField] private Transform _bottomSeatAnchor;
        [SerializeField] private Transform _rightSeatAnchor;
        [SerializeField] private Transform _topSeatAnchor;
        [SerializeField] private Transform _leftSeatAnchor;

        [Header("見た目")]
        [Tooltip("OFFにすると照らす演出を出さない（入れた場合と入れない場合の比較用）。")]
        [SerializeField] private bool _enabled = true;
        [Tooltip("中心の色（アルファが明るさ。外側へ向かって透明になる）。")]
        [SerializeField] private Color _color = new Color(1f, 0.96f, 0.82f, 0.22f);
        [Tooltip("下座席（手札が大きい）の楕円の大きさ（手札の並ぶ方向の幅, 奥行き）。")]
        [SerializeField] private Vector2 _bottomSize = new Vector2(3.9f, 1.3f);
        [Tooltip("ほかの座席の楕円の大きさ（手札の並ぶ方向の幅, 奥行き）。")]
        [SerializeField] private Vector2 _otherSize = new Vector2(3.2f, 1.0f);
        [Tooltip("フェルト面（座席アンカーの高さ）から、どれだけ下に置くか。カードより下、フェルトより上になるようにする。")]
        [SerializeField] private float _belowAnchor = 0.0015f;
        [Tooltip("手番が変わったときに、次の座席へ移る（消える・現れる）時間（秒）。")]
        [SerializeField] private float _fadeSeconds = 0.3f;

        private readonly MeshRenderer[] _spots = new MeshRenderer[4];
        private readonly float[] _alphas = new float[4];
        private int _current = -1;
        private Material _material;

        /// <summary>手番の座席を照らす（null で消す）。</summary>
        public void SetSeat(CameraDirector.SeatSide? side)
        {
            _current = side.HasValue ? (int)side.Value : -1;
        }

        private void Awake()
        {
            _material = new Material(Shader.Find("Sprites/Default"));
            var anchors = new[] { _bottomSeatAnchor, _rightSeatAnchor, _topSeatAnchor, _leftSeatAnchor };
            for (int i = 0; i < anchors.Length; i++)
            {
                if (anchors[i] != null)
                {
                    _spots[i] = BuildSpot(anchors[i], i == 0 ? _bottomSize : _otherSize, $"TurnSpotlight_{(CameraDirector.SeatSide)i}");
                    SetAlpha(i, 0f);
                }
            }
        }

        private void Update()
        {
            float step = Time.deltaTime / Mathf.Max(0.01f, _fadeSeconds);
            for (int i = 0; i < _spots.Length; i++)
            {
                if (_spots[i] == null)
                {
                    continue;
                }
                float target = _enabled && i == _current ? 1f : 0f;
                if (!Mathf.Approximately(_alphas[i], target))
                {
                    SetAlpha(i, Mathf.MoveTowards(_alphas[i], target, step));
                }
            }
        }

        private void SetAlpha(int index, float alpha)
        {
            _alphas[index] = alpha;
            var spot = _spots[index];
            spot.enabled = alpha > 0f;
            var block = new MaterialPropertyBlock();
            block.SetColor("_Color", new Color(1f, 1f, 1f, alpha));
            spot.SetPropertyBlock(block);
        }

        /// <summary>座席アンカーの直下に、アンカーの向き（手札の並ぶ方向）にそろえた楕円の平面を作る。</summary>
        private MeshRenderer BuildSpot(Transform anchor, Vector2 size, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = anchor.position - Vector3.up * _belowAnchor;
            go.transform.rotation = Quaternion.Euler(0f, anchor.eulerAngles.y, 0f);

            const int segments = 48;
            var verts = new Vector3[segments + 1];
            var colors = new Color[segments + 1];
            var tris = new int[segments * 3];
            verts[0] = Vector3.zero;
            colors[0] = _color;
            var edge = new Color(_color.r, _color.g, _color.b, 0f);
            for (int s = 0; s < segments; s++)
            {
                float a = s * Mathf.PI * 2f / segments;
                verts[s + 1] = new Vector3(Mathf.Cos(a) * size.x / 2f, 0f, Mathf.Sin(a) * size.y / 2f);
                colors[s + 1] = edge;
                // 上から見て時計回りに並べて、上向きの面にする。
                tris[s * 3] = 0;
                tris[s * 3 + 1] = ((s + 1) % segments) + 1;
                tris[s * 3 + 2] = s + 1;
            }

            var mesh = new Mesh { name = "TurnSpotlightMesh" };
            mesh.vertices = verts;
            mesh.colors = colors;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }
    }
}
