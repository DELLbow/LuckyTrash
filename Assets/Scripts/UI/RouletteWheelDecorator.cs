using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace LuckyTrash.UI
{
    /// <summary>
    /// ルーレット（<see cref="RouletteWheelView"/>）の見た目だけを強化するコンポーネント。
    /// 抽選・回転の仕組み（扇形のインデックスと角度の対応、<see cref="RouletteAngleMath"/>）には一切関与しない。
    /// 起動時（および Editor 上では <see cref="Rebuild"/> 経由）に、外枠（リム）・仕切り線・中心キャップ・
    /// 立体的なポインターをプリミティブ/コード生成メッシュで作成し、6枚のラベルの向きを扇形ごとに補正する。
    /// </summary>
    [ExecuteAlways]
    public class RouletteWheelDecorator : MonoBehaviour
    {
        /// <summary>
        /// リムの配色案。Customを選ぶと_rimColor等の値をそのまま使う。
        /// A/B/Cは比較用のプリセットで、Rebuild時に_rimColor等を上書きする。
        /// </summary>
        public enum RimColorPreset
        {
            Custom,
            A_GoldMetallic,
            B_BlackWithGoldLine,
            C_Ivory
        }

        private const float SliceAngle = RouletteAngleMath.SliceAngle;
        /// <summary>円盤（扇形メッシュ）の半径。Editor専用の焼き込みツール(RouletteFaceBaker)からも参照する。</summary>
        public const float DiscRadius = 0.6f;

        [Header("参照")]
        [Tooltip("回転する円盤（RouletteWheelView._discと同じTransform）。")]
        [SerializeField] private Transform _disc;
        [Tooltip("固定のポインター（既存のPointer3D）。メッシュを立体的な三角形に差し替える。")]
        [SerializeField] private Transform _pointer;
        [Tooltip("円盤の下の厚みのある土台（既存のBase）。メッシュを円柱に差し替えて使う。")]
        [SerializeField] private Transform _base;
        [Tooltip("6枚のラベル（Label_0〜Label_5、Discの子。順序はスライスのインデックス順と一致していること）。")]
        [SerializeField] private List<TMP_Text> _sliceLabels = new List<TMP_Text>();

        [Header("ラベル（焼き込み前の3Dラベル。焼き込み後は非表示）")]
        [Tooltip("全ラベル共通のフォントサイズ。扇形からはみ出さない範囲で調整する。")]
        [SerializeField] private float _labelFontSize = 1.3f;
        [Tooltip("縦書きラベル（文字を積んだブロック）の中心を、円盤中心からどれだけ離すか。0=中心、0.6=リム際。")]
        [SerializeField] private float _labelRadius = 0.37f;
        [Tooltip("縦書きの行間調整（TMPのlineSpacing、%）。負値で文字同士を詰める、正値で広げる。")]
        [SerializeField] private float _labelLineSpacing = 0f;

        [Header("焼き込みフェイス")]
        [Tooltip("マス・縦書きラベル・仕切り線を1枚に焼き込んだテクスチャ。Editor専用のRouletteFaceBakerで生成/再生成する。")]
        [SerializeField] private Texture2D _bakedFaceTexture;
        [Tooltip("焼き込みテクスチャの解像度（正方形、ピクセル）。RouletteFaceBakerが参照する。")]
        [SerializeField] private int _bakeResolution = 1024;
        [Tooltip("焼き込みPNGの保存先（Assets相対パス）。RouletteFaceBakerが参照する。")]
        [SerializeField] private string _bakeOutputPath = "Assets/Playing Cards/Resource/Textures/RouletteFace_Baked.png";
        [Tooltip("trueの場合、焼き込み済みテクスチャがあるとき、元の扇形メッシュ・3Dラベル・仕切り線・中心キャップを非表示にする（削除はしない。再焼き込み用に残す）。")]
        [SerializeField] private bool _hideOriginalFaceContent = true;
        [Tooltip("円盤外周に焼き込む暗いリングの幅（ワールド単位）。別メッシュを追加せず、テクスチャに描き込んでリムとの境目に接地感を出す。RouletteFaceBakerが参照する。")]
        [SerializeField] private float _edgeRingWidth = 0.03f;
        [Tooltip("円盤外周の暗いリングの明るさ倍率（外周端で何倍暗くするか）。RouletteFaceBakerが参照する。")]
        [Range(0f, 1f)] [SerializeField] private float _edgeRingDarkness = 0.55f;

        [Header("台座（Pedestal、既存のBaseを円柱メッシュに差し替え）")]
        [SerializeField] private float _pedestalRadius = 0.70f;
        [SerializeField] private float _pedestalHeight = 0.12f;
        [Tooltip("円盤（_disc）がテーブル面からどれだけ浮くか（ローカルY）。台座の上面＋わずかな隙間が目安。")]
        [SerializeField] private float _discHeightAboveTable = 0.13f;
        [Tooltip("台座底面のローカルY（root基準、通常は負の小さい値）。台座の接地はrootのY座標で合わせる" +
            "（フェルト面はY≈1.7314。テーブル縁のレール上面Y≈1.819と混同しないこと）。")]
        [SerializeField] private float _tableContactOffset = -0.008f;
        [Tooltip("台座とテーブル面の境目に置く、外側へ向かって薄くなる暗いリング（接地影）の幅（ワールド単位）。0で生成しない。")]
        [SerializeField] private float _contactShadowWidth = 0.08f;
        [Tooltip("接地影リングの内側（台座の根元）での不透明度。")]
        [Range(0f, 1f)] [SerializeField] private float _contactShadowAlpha = 0.5f;

        [Header("外枠（リム）")]
        [Tooltip("Custom以外を選ぶと、Rebuild時に下の色/金属性の値を比較用プリセットで上書きする（どれにするか未確定の比較用）。台座にも同じ色が使われる。")]
        [SerializeField] private RimColorPreset _rimColorPreset = RimColorPreset.Custom;
        [Tooltip("リム内側の半径。円盤半径(0.6)とほぼ同じにして、円盤とリムの間に隙間ができないようにする（内部で円盤半径未満にはならないようクランプする）。")]
        [SerializeField] private float _rimInnerRadius = 0.61f;
        [SerializeField] private float _rimWidth = 0.09f;
        [SerializeField] private float _rimHeight = 0.09f;
        [Tooltip("リム上面の外側・内側の角の丸み（半径、ワールド単位）。外壁・上面・内壁を1枚のなめらかな面でつなぐ。")]
        [SerializeField] private float _rimBevelRadius = 0.008f;
        [Tooltip("角の丸みの分割数。")]
        [SerializeField] private int _rimBevelSteps = 3;
        [Tooltip("円周の分割数。64以上を推奨（滑らかな円柱に見えるように）。台座にも同じ値を使う。")]
        [SerializeField] private int _rimSegments = 64;
        [SerializeField] private Material _rimMaterial;
        [SerializeField] private Color _rimColor = new Color(0.45f, 0.30f, 0.18f, 1f);
        [Range(0f, 1f)] [SerializeField] private float _rimMetallic = 0.15f;
        [Range(0f, 1f)] [SerializeField] private float _rimSmoothness = 0.35f;
        [Tooltip("リム内壁の明るさ倍率（1=上面と同じ、小さいほど暗い）。円盤との境目に接地感を出す。")]
        [Range(0.3f, 1f)] [SerializeField] private float _rimInnerWallBrightness = 0.65f;        [Tooltip("リム内側の縁に細いアクセントラインを追加するか（案Bの金ラインなど）。")]
        [SerializeField] private bool _rimAccentEnabled = false;
        [SerializeField] private Color _rimAccentColor = new Color(0.83f, 0.68f, 0.21f, 1f);
        [SerializeField] private float _rimAccentWidth = 0.015f;
        [Range(0f, 1f)] [SerializeField] private float _rimAccentMetallic = 0.7f;
        [Range(0f, 1f)] [SerializeField] private float _rimAccentSmoothness = 0.6f;

        [Header("仕切り線")]
        [SerializeField] private float _dividerThickness = 0.015f;
        [SerializeField] private float _dividerHeight = 0.012f;
        [SerializeField] private float _dividerInnerRadius = 0.04f;

        [Header("中心キャップ")]
        [SerializeField] private bool _showCenterCap = true;
        [Tooltip("trueだと円盤と一緒に回転し、falseだと外枠と同じく固定される。")]
        [SerializeField] private bool _capRotatesWithDisc = true;
        [SerializeField] private float _capRadius = 0.05f;
        [SerializeField] private float _capHeight = 0.03f;

        [Header("停止ハイライト（半透明オーバーレイ）")]
        [Tooltip("止まった扇形の上に重ねる、半透明オーバーレイの色（アルファはRouletteWheelViewが演出時に書き換える）。")]
        [SerializeField] private Color _highlightOverlayColor = new Color(1f, 0.95f, 0.5f, 0f);

        [Header("ポインター")]
        [SerializeField] private float _pointerWidth = 0.18f;
        [SerializeField] private float _pointerLength = 0.22f;
        [SerializeField] private float _pointerHeight = 0.07f;
        [Tooltip("ポインター先端が円盤の上にどれだけかかるか（ワールド単位、円盤中心方向へのオフセット）。")]
        [SerializeField] private float _pointerOverlapWithDisc = 0.08f;

        private const string GeneratedRootName = "GeneratedDecorations";
        private Transform _generatedRoot;
        private Transform _generatedDiscRoot;
        private Material _runtimeRimMaterial;
        private Material _runtimeRimAccentMaterial;
        private Material _runtimeRimInnerWallMaterial;

        private void Awake()
        {
            Rebuild();
        }

        /// <summary>
        /// 外枠・仕切り線・キャップ・ポインターを再生成し、ラベルの向きを補正する。
        /// 既存の生成物があれば一度破棄してから作り直す（Inspectorでの値変更後に呼び直せる）。
        /// </summary>
        [ContextMenu("Rebuild")]
        public void Rebuild()
        {
            if (_disc == null)
            {
                return;
            }

            ApplyRimColorPreset();
            CleanupGenerated();
            FixLabelOrientations();
            BuildPedestal();
            BuildContactShadow();
            BuildRim();
            BuildDividers();
            BuildCenterCap();
            BuildBakedFace();
            BuildHighlightOverlay();
            ApplyOriginalFaceVisibility();
            BuildPointer();

            _disc.localPosition = new Vector3(_disc.localPosition.x, _discHeightAboveTable + _tableContactOffset, _disc.localPosition.z);
        }

        /// <summary>
        /// リム内側半径を、円盤(半径DiscRadius)に対して最低限のクリアランスを持つ値にクランプする。
        /// Inspectorで小さい値を入れても、円盤とリムが干渉しないようにするための安全装置。
        /// </summary>
        private float GetClampedRimInnerRadius()
        {
            const float clearance = 0.01f;
            return Mathf.Max(_rimInnerRadius, DiscRadius + clearance);
        }

        /// <summary>
        /// _rimColorPresetがCustom以外のとき、比較用の配色プリセットで色/金属性/アクセントラインを上書きする。
        /// </summary>
        private void ApplyRimColorPreset()
        {
            switch (_rimColorPreset)
            {
                case RimColorPreset.A_GoldMetallic:
                    _rimColor = new Color(0.78f, 0.62f, 0.22f, 1f);
                    _rimMetallic = 0.85f;
                    _rimSmoothness = 0.75f;
                    _rimAccentEnabled = false;
                    break;
                case RimColorPreset.B_BlackWithGoldLine:
                    _rimColor = new Color(0.07f, 0.07f, 0.08f, 1f);
                    _rimMetallic = 0.3f;
                    _rimSmoothness = 0.5f;
                    _rimAccentEnabled = true;
                    _rimAccentColor = new Color(0.83f, 0.68f, 0.21f, 1f);
                    _rimAccentMetallic = 0.85f;
                    _rimAccentSmoothness = 0.75f;
                    break;
                case RimColorPreset.C_Ivory:
                    _rimColor = new Color(0.90f, 0.87f, 0.78f, 1f);
                    _rimMetallic = 0.05f;
                    _rimSmoothness = 0.4f;
                    _rimAccentEnabled = false;
                    break;
                case RimColorPreset.Custom:
                default:
                    break;
            }

            // 色/金属性が変わった可能性があるため、キャッシュ済みランタイムマテリアルを作り直す。
            _runtimeRimMaterial = null;
            _runtimeRimAccentMaterial = null;
            _runtimeRimInnerWallMaterial = null;
        }

        private void CleanupGenerated()
        {
            var existingRoot = transform.Find(GeneratedRootName);
            if (existingRoot != null)
            {
                DestroyImmediateOrRuntime(existingRoot.gameObject);
            }

            var existingDiscRoot = _disc.Find(GeneratedRootName);
            if (existingDiscRoot != null)
            {
                DestroyImmediateOrRuntime(existingDiscRoot.gameObject);
            }

            _generatedRoot = new GameObject(GeneratedRootName).transform;
            _generatedRoot.SetParent(transform, false);

            _generatedDiscRoot = new GameObject(GeneratedRootName).transform;
            _generatedDiscRoot.SetParent(_disc, false);
        }

        private static void DestroyImmediateOrRuntime(Object obj)
        {
            if (Application.isPlaying)
            {
                Destroy(obj);
            }
            else
            {
                DestroyImmediate(obj);
            }
        }

        /// <summary>
        /// 各ラベルの向きを、扇形の中心線に沿った放射方向へ補正し、1文字ずつ縦に積んだ
        /// 縦書き表示に組み替える。Discのローカル角度φ_i = i*60+30 だけ追加でY回転させることで、
        /// その扇形がポインター位置で止まったときに、どのインデックスでも同一のワールド向き
        /// （プレイヤーから見て正しく読める向き）になる。
        /// </summary>
        private void FixLabelOrientations()
        {
            for (int i = 0; i < _sliceLabels.Count && i < 6; i++)
            {
                var label = _sliceLabels[i];
                if (label == null)
                {
                    continue;
                }

                float phi = i * SliceAngle + SliceAngle / 2f;

                // 90度X回転で円盤に寝かせた上で、扇形の中心角 phi 分だけY回転させると、
                // その扇形がポインター位置で止まったときに全インデックス共通のワールド向きになる
                // （実機検証済み）。+180度しているのは、基準となるワールド向きが
                // そのままでは上下反転して見えるため、文字が正しく読める向きに補正するもの。
                label.transform.localRotation = Quaternion.Euler(90f, phi + 180f, 0f);

                // 中心→外周方向(放射方向)に沿って、ラベルブロックの位置を再配置する。
                // 高さ(ローカルY)は既存の値を保持し、XZのみ角度φ・半径_labelRadiusから再計算する。
                Vector3 radialDir = Quaternion.Euler(0f, phi, 0f) * Vector3.forward;
                Vector3 pos = radialDir * _labelRadius;
                pos.y = label.transform.localPosition.y;
                label.transform.localPosition = pos;

                string baseText = StripToBaseCharacters(label.text);
                label.text = BuildVerticalLabelText(baseText);
                label.alignment = TextAlignmentOptions.Center;
                label.fontSize = _labelFontSize;
                label.lineSpacing = _labelLineSpacing;
                label.enableAutoSizing = false;
            }
        }

        /// <summary>
        /// 既にこのメソッドで縦書き化された文字列(改行や&lt;rotate&gt;タグを含む)からでも、
        /// 元の1行分の文字（例："数字"）を復元する。Rebuildを複数回呼んでも結果が変わらないようにするため。
        /// </summary>
        private static string StripToBaseCharacters(string text)
        {
            var sb = new StringBuilder();
            bool inTag = false;
            foreach (char c in text)
            {
                if (c == '<') { inTag = true; continue; }
                if (c == '>') { inTag = false; continue; }
                if (inTag) { continue; }
                if (c == '\n') { continue; }
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>
        /// "数字"のような1行の文字列を、1文字ずつ改行で積んだ縦書き用リッチテキストに変換する。
        /// 長音記号「ー」はそのままだと横棒に見えてしまうため、90度回転させて縦棒にする。
        /// </summary>
        private static string BuildVerticalLabelText(string baseText)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < baseText.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append('\n');
                }

                char c = baseText[i];
                if (c == 'ー')
                {
                    sb.Append("<rotate=90>ー</rotate>");
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// 土台（Base）のメッシュを、厚みのある円柱（側面＋上面）に差し替える。
        /// リムが上面の外周にぴったり乗るよう、半径はリム外径を下回らないようクランプする。
        /// 円盤と同じく頂点カラーではなくLitマテリアル（GetOrCreateRimMaterial）を使うため、側面に陰影が付く。
        /// </summary>
        private void BuildPedestal()
        {
            if (_base == null)
            {
                return;
            }

            float radius = GetPedestalRadius();
            float bottomY = _tableContactOffset;
            float topY = bottomY + Mathf.Max(0.001f, _pedestalHeight);
            int seg = Mathf.Max(8, _rimSegments);

            var mesh = new Mesh { name = "RoulettePedestalMesh" };
            var verts = new List<Vector3>();
            var tris = new List<int>();

            // 側面（外向き法線）。円周方向に頂点を共有したスムーズなリング状ストリップにする。
            AddSmoothRing(verts, tris, seg, radius, bottomY, radius, topY);

            // 上面（扇形ファン、中心を共有）。平面なので頂点共有の有無は陰影に影響しない。
            int centerIndex = verts.Count;
            verts.Add(new Vector3(0f, topY, 0f));
            for (int s = 0; s < seg; s++)
            {
                float a = s * 360f / seg * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                verts.Add(dir * radius + Vector3.up * topY);
            }
            for (int s = 0; s < seg; s++)
            {
                int ringS = centerIndex + 1 + s;
                int ringNext = centerIndex + 1 + ((s + 1) % seg);
                tris.Add(centerIndex);
                tris.Add(ringS);
                tris.Add(ringNext);
            }

            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var mf = _base.GetComponent<MeshFilter>();
            if (mf == null)
            {
                mf = _base.gameObject.AddComponent<MeshFilter>();
            }
            mf.sharedMesh = mesh;

            var mr = _base.GetComponent<MeshRenderer>();
            if (mr == null)
            {
                mr = _base.gameObject.AddComponent<MeshRenderer>();
            }
            mr.sharedMaterial = GetOrCreateRimMaterial();

            _base.localPosition = Vector3.zero;
            _base.localScale = Vector3.one;
        }

        /// <summary>
        /// 台座の半径。リムが上面の外周にぴったり乗るよう、リム外径を下回らないようクランプする。
        /// </summary>
        private float GetPedestalRadius()
        {
            float rimOuterRadius = GetClampedRimInnerRadius() + _rimWidth;
            return Mathf.Max(_pedestalRadius, rimOuterRadius);
        }

        /// <summary>
        /// 台座の根元のテーブル面に、外側へ向かって透明になる暗いリング（接地影）を置く。root直下（非回転）。
        /// ポストプロセスのアンビエントオクルージョンを使わずに、台座とテーブルの境目へ接地感を出すためのもの。
        /// 頂点カラーのアルファでグラデーションさせ、ライティングの影響を受けないSprites/Defaultで描く。
        /// </summary>
        private void BuildContactShadow()
        {
            if (_base == null || _contactShadowWidth <= 0f || _contactShadowAlpha <= 0f)
            {
                return;
            }

            var go = new GameObject("ContactShadow");
            go.transform.SetParent(_generatedRoot, false);
            // テーブル面（台座底面と同じ高さ）とのZファイティングを避けるため、わずかに浮かせる。
            go.transform.localPosition = new Vector3(0f, _tableContactOffset + 0.0015f, 0f);

            float innerR = GetPedestalRadius();
            float outerR = innerR + _contactShadowWidth;
            int seg = Mathf.Max(8, _rimSegments);

            var verts = new List<Vector3>(seg * 2);
            var colors = new List<Color>(seg * 2);
            var tris = new List<int>(seg * 6);
            var innerColor = new Color(0f, 0f, 0f, _contactShadowAlpha);
            var outerColor = new Color(0f, 0f, 0f, 0f);

            for (int s = 0; s < seg; s++)
            {
                float a = s * 360f / seg * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                verts.Add(dir * outerR);
                colors.Add(outerColor);
                verts.Add(dir * innerR);
                colors.Add(innerColor);
            }

            // 外側→内側の順で並べると上向きの面になる（AddSmoothRingと同じ並び）。
            for (int s = 0; s < seg; s++)
            {
                int next = (s + 1) % seg;
                int a = s * 2;
                int b = next * 2;
                int c = next * 2 + 1;
                int d = s * 2 + 1;
                tris.Add(a); tris.Add(b); tris.Add(c);
                tris.Add(a); tris.Add(c); tris.Add(d);
            }

            var mesh = new Mesh { name = "RouletteContactShadowMesh" };
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        /// <summary>
        /// 半径radius1・高さy1の円と、半径radius2・高さy2の円を結ぶ、円周方向に頂点を共有した
        /// （継ぎ目のない）リング状のストリップメッシュを追加する。頂点を共有するため
        /// RecalculateNormalsで隣接面の法線が平均化され、なめらかな陰影になる
        /// （BuildPedestalの側面と上面の境目では、面ごとに別々に頂点を作ることで角を残す）。
        /// 生成される四角形の頂点順は、既存のAddQuad(a,b,c,d)で
        /// a=(radius1,y1)@s, b=(radius1,y1)@s+1, c=(radius2,y2)@s+1, d=(radius2,y2)@s
        /// を呼んだ場合と同じ面の向き（法線方向）になる。
        /// </summary>
        private static void AddSmoothRing(List<Vector3> verts, List<int> tris, int seg,
            float radius1, float y1, float radius2, float y2)
        {
            int baseIndex = verts.Count;
            for (int s = 0; s < seg; s++)
            {
                float a = s * 360f / seg * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                verts.Add(dir * radius1 + Vector3.up * y1);
                verts.Add(dir * radius2 + Vector3.up * y2);
            }

            for (int s = 0; s < seg; s++)
            {
                int next = (s + 1) % seg;
                int a = baseIndex + s * 2;
                int b = baseIndex + next * 2;
                int c = baseIndex + next * 2 + 1;
                int d = baseIndex + s * 2 + 1;
                tris.Add(a); tris.Add(b); tris.Add(c);
                tris.Add(a); tris.Add(c); tris.Add(d);
            }
        }

        private Material GetOrCreateRimMaterial()
        {
            if (_rimMaterial != null)
            {
                return _rimMaterial;
            }

            if (_runtimeRimMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                _runtimeRimMaterial = new Material(shader) { color = _rimColor };
                if (_runtimeRimMaterial.HasProperty("_Metallic"))
                {
                    _runtimeRimMaterial.SetFloat("_Metallic", _rimMetallic);
                }
                if (_runtimeRimMaterial.HasProperty("_Smoothness"))
                {
                    _runtimeRimMaterial.SetFloat("_Smoothness", _rimSmoothness);
                }
            }
            return _runtimeRimMaterial;
        }

        private Material GetOrCreateRimAccentMaterial()
        {
            if (_runtimeRimAccentMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                _runtimeRimAccentMaterial = new Material(shader) { color = _rimAccentColor };
                if (_runtimeRimAccentMaterial.HasProperty("_Metallic"))
                {
                    _runtimeRimAccentMaterial.SetFloat("_Metallic", _rimAccentMetallic);
                }
                if (_runtimeRimAccentMaterial.HasProperty("_Smoothness"))
                {
                    _runtimeRimAccentMaterial.SetFloat("_Smoothness", _rimAccentSmoothness);
                }
            }
            return _runtimeRimAccentMaterial;
        }

        /// <summary>
        /// 円盤の外周に、厚みのある固定リング（外枠）を生成する。root直下（非回転）。
        /// 外壁→外側の角（丸み）→上面→内側の角（丸み）→内壁、という1本の断面を円周方向に回した
        /// 1つのつながったメッシュにする（底面は台座に接するため省略）。断面上の頂点を共有し、
        /// 法線も断面の形から直接与えるため、面の境目に継ぎ目ができず、角も丸みの範囲だけなめらかに曲がる。
        /// 内壁だけは円盤との境目に接地感を出すため、少し暗い色のサブメッシュ1にする（頂点は共有のまま）。
        /// </summary>
        private void BuildRim()
        {
            var go = new GameObject("Rim");
            go.transform.SetParent(_generatedRoot, false);
            go.transform.localPosition = Vector3.zero;

            float innerR = GetClampedRimInnerRadius();
            float outerR = innerR + _rimWidth;
            float baseY = _tableContactOffset + _pedestalHeight;
            float h = baseY + _rimHeight;
            int seg = Mathf.Max(8, _rimSegments);
            float bevel = Mathf.Clamp(_rimBevelRadius, 0.0005f, Mathf.Min(_rimWidth, _rimHeight) * 0.45f);
            int bevelSteps = Mathf.Max(1, _rimBevelSteps);

            // 断面(r, y)と、その点での法線(nr, ny)。この順（外壁を上へ→上面を内側へ→内壁を下へ）に
            // 円周方向へ回すと、全ての面が外側（見える側）を向く。
            var profile = new List<Vector2>();
            var normals2D = new List<Vector2>();
            int innerWallStart;

            profile.Add(new Vector2(outerR, baseY));
            normals2D.Add(new Vector2(1f, 0f));

            // 外側の角：中心(outerR-bevel, h-bevel)、0°(外向き)→90°(上向き)
            for (int i = 0; i <= bevelSteps; i++)
            {
                float t = i / (float)bevelSteps * 90f * Mathf.Deg2Rad;
                var n = new Vector2(Mathf.Cos(t), Mathf.Sin(t));
                profile.Add(new Vector2(outerR - bevel, h - bevel) + n * bevel);
                normals2D.Add(n);
            }

            // 内側の角：中心(innerR+bevel, h-bevel)、90°(上向き)→180°(内向き)
            for (int i = 0; i <= bevelSteps; i++)
            {
                float t = (90f + i / (float)bevelSteps * 90f) * Mathf.Deg2Rad;
                var n = new Vector2(Mathf.Cos(t), Mathf.Sin(t));
                profile.Add(new Vector2(innerR + bevel, h - bevel) + n * bevel);
                normals2D.Add(n);
            }
            innerWallStart = profile.Count - 1;

            profile.Add(new Vector2(innerR, baseY));
            normals2D.Add(new Vector2(-1f, 0f));

            var verts = new List<Vector3>(profile.Count * seg);
            var normals = new List<Vector3>(profile.Count * seg);
            for (int k = 0; k < profile.Count; k++)
            {
                for (int s = 0; s < seg; s++)
                {
                    float a = s * 360f / seg * Mathf.Deg2Rad;
                    Vector3 dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                    verts.Add(dir * profile[k].x + Vector3.up * profile[k].y);
                    normals.Add((dir * normals2D[k].x + Vector3.up * normals2D[k].y).normalized);
                }
            }

            var trisMain = new List<int>();
            var trisInnerWall = new List<int>();
            for (int k = 0; k < profile.Count - 1; k++)
            {
                var tris = k >= innerWallStart ? trisInnerWall : trisMain;
                for (int s = 0; s < seg; s++)
                {
                    int next = (s + 1) % seg;
                    int a = k * seg + s;
                    int b = k * seg + next;
                    int c = (k + 1) * seg + next;
                    int d = (k + 1) * seg + s;
                    tris.Add(a); tris.Add(b); tris.Add(c);
                    tris.Add(a); tris.Add(c); tris.Add(d);
                }
            }

            var mesh = new Mesh { name = "RouletteRimMesh" };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(trisMain, 0);
            mesh.SetTriangles(trisInnerWall, 1);
            mesh.RecalculateBounds();

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { GetOrCreateRimMaterial(), GetOrCreateRimInnerWallMaterial() };

            if (_rimAccentEnabled)
            {
                // 内側の角の丸みに浮かないよう、上面の平らな部分から始める。
                BuildRimAccentLine(innerR + bevel, h, seg);
            }
        }

        private Material GetOrCreateRimInnerWallMaterial()
        {
            if (_runtimeRimInnerWallMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                var darkened = _rimColor * _rimInnerWallBrightness;
                darkened.a = _rimColor.a;
                _runtimeRimInnerWallMaterial = new Material(shader) { color = darkened };
                if (_runtimeRimInnerWallMaterial.HasProperty("_Metallic"))
                {
                    _runtimeRimInnerWallMaterial.SetFloat("_Metallic", _rimMetallic);
                }
                if (_runtimeRimInnerWallMaterial.HasProperty("_Smoothness"))
                {
                    _runtimeRimInnerWallMaterial.SetFloat("_Smoothness", _rimSmoothness);
                }
            }
            return _runtimeRimInnerWallMaterial;
        }

        /// <summary>
        /// リムの内側の縁に、別マテリアルの細い帯（アクセントライン）を上面にわずかに重ねて配置する
        /// （案B：黒リム＋内側の細い金ライン、などの比較用）。
        /// </summary>
        private void BuildRimAccentLine(float rimInnerR, float rimHeight, int seg)
        {
            var go = new GameObject("RimAccentLine");
            go.transform.SetParent(_generatedRoot, false);
            go.transform.localPosition = Vector3.zero;

            var mesh = new Mesh { name = "RouletteRimAccentMesh" };
            float innerR = rimInnerR;
            float outerR = rimInnerR + _rimAccentWidth;
            // Zファイティングを避けるため、リム上面よりわずかに高い位置に重ねる。
            float h = rimHeight + 0.001f;

            var verts = new List<Vector3>();
            var tris = new List<int>();

            for (int s = 0; s < seg; s++)
            {
                float a0 = s * 360f / seg * Mathf.Deg2Rad;
                float a1 = (s + 1) * 360f / seg * Mathf.Deg2Rad;
                Vector3 dirA = new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0));
                Vector3 dirB = new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1));

                Vector3 innerTopA = dirA * innerR + Vector3.up * h;
                Vector3 innerTopB = dirB * innerR + Vector3.up * h;
                Vector3 outerTopA = dirA * outerR + Vector3.up * h;
                Vector3 outerTopB = dirB * outerR + Vector3.up * h;

                // 外側→内側の順で並べると上向きの面になる（逆にすると下向きになり、上から見えない）。
                AddQuad(verts, tris, outerTopA, outerTopB, innerTopB, innerTopA);
            }

            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = GetOrCreateRimAccentMaterial();
        }

        private static void AddQuad(List<Vector3> verts, List<int> tris, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int baseIndex = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            tris.Add(baseIndex); tris.Add(baseIndex + 1); tris.Add(baseIndex + 2);
            tris.Add(baseIndex); tris.Add(baseIndex + 2); tris.Add(baseIndex + 3);
        }

        /// <summary>
        /// 6つの扇形の境目（0,60,120,...,300度）に、放射状の細い仕切り線をCube primitiveで配置する。
        /// Discの子にして、円盤と一緒に回転させる。
        /// </summary>
        private void BuildDividers()
        {
            // 焼き込みテクスチャの撮影範囲(DiscRadius)をはみ出さないよう、円盤の縁までで止める。
            float length = DiscRadius - _dividerInnerRadius;
            float midRadius = _dividerInnerRadius + length / 2f;
            var rimMat = GetOrCreateRimMaterial();

            for (int i = 0; i < 6; i++)
            {
                float angle = i * SliceAngle;
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Divider_" + i;
                DestroyImmediateOrRuntime(go.GetComponent<Collider>());
                go.transform.SetParent(_generatedDiscRoot, false);
                go.transform.localScale = new Vector3(_dividerThickness, _dividerHeight, length);
                go.transform.localPosition = new Vector3(0f, 0.01f + _dividerHeight / 2f, 0f)
                    + Quaternion.Euler(0f, angle, 0f) * (Vector3.forward * midRadius);
                go.transform.localRotation = Quaternion.Euler(0f, angle, 0f);
                go.GetComponent<MeshRenderer>().sharedMaterial = rimMat;
            }
        }

        /// <summary>
        /// 円盤中心に小さな円柱のキャップを置く。Cylinder primitiveを使用。
        /// </summary>
        private void BuildCenterCap()
        {
            if (!_showCenterCap)
            {
                return;
            }

            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "CenterCap";
            DestroyImmediateOrRuntime(go.GetComponent<Collider>());
            var parent = _capRotatesWithDisc ? _generatedDiscRoot : _generatedRoot;
            go.transform.SetParent(parent, false);
            // Unityの標準Cylinderは高さ2・半径1が基準なので、スケールで実寸に合わせる。
            go.transform.localScale = new Vector3(_capRadius * 2f, _capHeight / 2f, _capRadius * 2f);
            go.transform.localPosition = new Vector3(0f, 0.01f + _capHeight / 2f, 0f);
            go.GetComponent<MeshRenderer>().sharedMaterial = GetOrCreateRimMaterial();
        }

        /// <summary>
        /// 焼き込み済みテクスチャ(_bakedFaceTexture)を貼った、円盤と同じ半径の円形メッシュを
        /// Discの子として1枚配置する。UVは "u=(x/DiscRadius)*0.5+0.5, v=(z/DiscRadius)*0.5+0.5" で、
        /// Editor専用のRouletteFaceBakerが撮影に使うカメラの向き（up=ローカル+Z, right=ローカル+X）と
        /// 完全に対応させている。マテリアルは不透明（URP Lit）で、描画順の問題が起きないようにする。
        /// </summary>
        private void BuildBakedFace()
        {
            if (_bakedFaceTexture == null)
            {
                return;
            }

            var go = new GameObject("BakedFace");
            go.transform.SetParent(_generatedDiscRoot, false);
            go.transform.localPosition = new Vector3(0f, 0.002f, 0f);

            var mesh = new Mesh { name = "RouletteBakedFaceMesh" };
            int seg = Mathf.Max(8, _rimSegments);

            var verts = new List<Vector3>(seg + 1);
            var uvs = new List<Vector2>(seg + 1);
            var tris = new List<int>(seg * 3);

            verts.Add(Vector3.zero);
            uvs.Add(new Vector2(0.5f, 0.5f));

            for (int s = 0; s <= seg; s++)
            {
                float a = s * 360f / seg * Mathf.Deg2Rad;
                float x = Mathf.Sin(a) * DiscRadius;
                float z = Mathf.Cos(a) * DiscRadius;
                verts.Add(new Vector3(x, 0f, z));
                uvs.Add(new Vector2(x / DiscRadius * 0.5f + 0.5f, z / DiscRadius * 0.5f + 0.5f));
            }

            for (int s = 1; s <= seg; s++)
            {
                tris.Add(0);
                tris.Add(s);
                tris.Add(s + 1);
            }

            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = GetOrCreateBakedFaceMaterial();
        }

        private Material GetOrCreateBakedFaceMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(shader) { mainTexture = _bakedFaceTexture };
            if (mat.HasProperty("_Metallic"))
            {
                mat.SetFloat("_Metallic", 0f);
            }
            if (mat.HasProperty("_Smoothness"))
            {
                mat.SetFloat("_Smoothness", 0.1f);
            }
            return mat;
        }

        /// <summary>
        /// 止まった扇形を光らせるための、半透明オーバーレイ用の扇形メッシュ(1マス分、-30°〜+30°)を
        /// Discの子として作る。初期状態はアルファ0（非表示）。RouletteWheelViewが、止まったスライスの
        /// bisector角度ぶんだけこのTransformをY回転させてから、アルファをフラッシュさせる。
        /// </summary>
        private void BuildHighlightOverlay()
        {
            var go = new GameObject("HighlightOverlay");
            go.transform.SetParent(_generatedDiscRoot, false);
            go.transform.localPosition = new Vector3(0f, 0.004f, 0f);
            go.transform.localRotation = Quaternion.identity;

            var mesh = new Mesh { name = "RouletteHighlightOverlayMesh" };
            int seg = Mathf.Max(4, _rimSegments / 6);
            float half = SliceAngle / 2f;

            var verts = new List<Vector3>(seg + 2) { Vector3.zero };
            var tris = new List<int>(seg * 3);

            for (int s = 0; s <= seg; s++)
            {
                float a = Mathf.Lerp(-half, half, s / (float)seg) * Mathf.Deg2Rad;
                verts.Add(new Vector3(Mathf.Sin(a) * DiscRadius, 0f, Mathf.Cos(a) * DiscRadius));
            }

            for (int s = 1; s <= seg; s++)
            {
                tris.Add(0);
                tris.Add(s);
                tris.Add(s + 1);
            }

            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = GetOrCreateHighlightOverlayMaterial();
        }

        private Material GetOrCreateHighlightOverlayMaterial()
        {
            // URP Lit/Unlit の透明化はScriptだけでは正しく反映されないことがあるため、
            // このプロジェクトの扇形(Slice_i)で実績のあるSprites/Default（アルファブレンド・ZWriteなし）を使う。
            var shader = Shader.Find("Sprites/Default");
            var mat = new Material(shader) { color = _highlightOverlayColor };
            return mat;
        }

        /// <summary>
        /// 焼き込み済みテクスチャがある場合、元の扇形メッシュ・3Dラベル・仕切り線・中心キャップを非表示にする
        /// （_hideOriginalFaceContentがtrueのとき）。削除はしないため、再焼き込みにそのまま使える。
        /// </summary>
        private void ApplyOriginalFaceVisibility()
        {
            bool hide = _hideOriginalFaceContent && _bakedFaceTexture != null;

            for (int i = 0; i < 6; i++)
            {
                var slice = _disc.Find("Slice_" + i);
                if (slice != null)
                {
                    slice.gameObject.SetActive(!hide);
                }
            }

            for (int i = 0; i < _sliceLabels.Count && i < 6; i++)
            {
                var label = _sliceLabels[i];
                if (label != null)
                {
                    label.gameObject.SetActive(!hide);
                }
            }

            for (int i = 0; i < 6; i++)
            {
                var divider = _generatedDiscRoot.Find("Divider_" + i);
                if (divider != null)
                {
                    divider.gameObject.SetActive(!hide);
                }
            }

            var cap = _generatedDiscRoot.Find("CenterCap");
            if (cap == null && _generatedRoot != null)
            {
                cap = _generatedRoot.Find("CenterCap");
            }
            if (cap != null)
            {
                cap.gameObject.SetActive(!hide);
            }
        }

        /// <summary>
        /// 固定ポインターのメッシュを、外枠の上に乗った立体的な三角（先端が円盤側）に差し替える。
        /// 位置・向きは既存のPointer3Dのワールド位置（プレイヤー手前側）を基準に、
        /// 先端が円盤上に少しかかるよう再配置する。
        /// </summary>
        private void BuildPointer()
        {
            if (_pointer == null)
            {
                return;
            }

            var mf = _pointer.GetComponent<MeshFilter>();
            if (mf == null)
            {
                mf = _pointer.gameObject.AddComponent<MeshFilter>();
            }

            var mesh = new Mesh { name = "RoulettePointerMesh" };
            float halfW = _pointerWidth / 2f;
            float len = _pointerLength;
            float h = _pointerHeight;

            // ローカル+Z方向（ルーレット中心へ向かう方向）に先端が来る三角柱。
            Vector3 tip = new Vector3(0f, 0f, len);
            Vector3 baseLeft = new Vector3(-halfW, 0f, 0f);
            Vector3 baseRight = new Vector3(halfW, 0f, 0f);
            Vector3 tipTop = tip + Vector3.up * h;
            Vector3 baseLeftTop = baseLeft + Vector3.up * h;
            Vector3 baseRightTop = baseRight + Vector3.up * h;

            var verts = new List<Vector3>();
            var tris = new List<int>();

            // 底面（下向き）
            AddTri(verts, tris, baseLeft, tip, baseRight);
            // 上面（上向き）
            AddTri(verts, tris, baseRightTop, tipTop, baseLeftTop);
            // 側面1（左）
            AddQuad(verts, tris, baseLeft, baseLeftTop, tipTop, tip);
            // 側面2（右）
            AddQuad(verts, tris, tip, tipTop, baseRightTop, baseRight);
            // 背面（底辺側）
            AddQuad(verts, tris, baseRight, baseRightTop, baseLeftTop, baseLeft);

            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;

            var mr = _pointer.GetComponent<MeshRenderer>();
            if (mr == null)
            {
                mr = _pointer.gameObject.AddComponent<MeshRenderer>();
            }
            mr.sharedMaterial = mr.sharedMaterial != null ? mr.sharedMaterial : GetOrCreateRimMaterial();

            // 先端(ローカル+Z)が円盤中心側へ向くよう、外枠の外側から円盤の上に少しかかる位置へ配置する。
            // 既存Pointer3Dのワールド位置（root基準でローカルZ=+0.708）を踏襲しつつ、
            // 先端が円盤(半径0.6)の縁から _pointerOverlapWithDisc だけ内側に入るよう根元位置を調整する。
            float tipWorldZ = DiscRadius - _pointerOverlapWithDisc; // 先端のroot基準ローカルZ
            float baseWorldZ = tipWorldZ + len; // 根元(外枠の外側)のroot基準ローカルZ
            _pointer.localPosition = new Vector3(0f, _tableContactOffset + _pedestalHeight + _rimHeight, baseWorldZ);
            _pointer.localRotation = Quaternion.Euler(0f, 180f, 0f); // ローカル+Zが円盤中心(root原点)を向くように反転
            _pointer.localScale = Vector3.one;
        }

        private static void AddTri(List<Vector3> verts, List<int> tris, Vector3 a, Vector3 b, Vector3 c)
        {
            int baseIndex = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            tris.Add(baseIndex); tris.Add(baseIndex + 1); tris.Add(baseIndex + 2);
        }
    }
}
