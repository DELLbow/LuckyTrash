using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace LuckyTrash.UI
{
    /// <summary>
    /// テーブルの周りの、暗いカジノのフロア（奥の壁と金の装飾ライン、島ごとに置かれたスロットマシン、
    /// 薄暗いブラックジャックのテーブル、深い赤のカーペット、テーブルの周りの光だまりと真下の影）を作る背景。
    /// あくまで脇役で、テーブルとUIを引き立たせるためのもの。
    /// 部屋の物はライト・環境光・影の影響を受けない専用の Unlit シェーダーで描くので、テーブルを照らしている
    /// Directional Light や環境光の設定には一切触れずに、背景だけを暗くできる（色は頂点カラーで決まり、
    /// テーブルから遠いほどカメラの背景色に溶け込む）。スロットマシンの機種名とリールの文字は 3D の TextMeshPro
    /// （ライトの影響を受けない Distance Field シェーダー）で描く。
    /// 形は起動時にコードで組み立て、マテリアルごとに1つのメッシュにまとめる（シーンには保存しない）。
    /// スロットマシンの光る部分と文字は、普段はばらばらのタイミングでゆっくり明るさが変わり、
    /// <see cref="PlayJackpot"/>（誰かがあがったとき）で一斉にパッと明るくなってから元の明るさへ戻る。
    /// </summary>
    [ExecuteAlways]
    public class CasinoBackgroundView : MonoBehaviour
    {
        /// <summary>スロットマシンの島（2〜4台を横に並べた一かたまり）。</summary>
        [Serializable]
        public struct SlotIsland
        {
            [Tooltip("島の中心のX。")] public float X;
            [Tooltip("島の正面のZ（壁からの距離を島ごとに変える）。")] public float Z;
            [Tooltip("島の向き（度）。正の値で正面が右（+X）を向く。")] public float Yaw;
            [Tooltip("台数。")] public int Count;
        }

        /// <summary>リールの絵柄（文字）と色。</summary>
        [Serializable]
        public struct ReelSymbol
        {
            public string Text;
            public Color Color;
        }

        /// <summary>別のテーブル（ブラックジャック用の半円形）。</summary>
        [Serializable]
        public struct OtherTable
        {
            [Tooltip("テーブルの中心（X, Z）。")] public Vector2 Position;
            [Tooltip("向き（度）。0で丸い側（プレイヤー側）がカメラを向く。")] public float Yaw;
        }

        [Header("マテリアル")]
        [Tooltip("壁・スロットマシンの本体・別のテーブル（LuckyTrash/CasinoBackgroundUnlit、遠いほど暗闇に溶ける）。")]
        [SerializeField] private Material _roomMaterial;
        [Tooltip("スロットマシンの光る部分（LuckyTrash/CasinoBackgroundUnlit、暗闇に溶けない設定）。")]
        [SerializeField] private Material _lampMaterial;
        [Tooltip("床のカーペット（LuckyTrash/CasinoCarpet）。")]
        [SerializeField] private Material _carpetMaterial;
        [Tooltip("ランプのにじみ・床やテーブルの光だまり（LuckyTrash/CasinoGlow、加算）。")]
        [SerializeField] private Material _glowMaterial;
        [Tooltip("テーブルの真下の影（LuckyTrash/CasinoShadow、半透明の黒）。")]
        [SerializeField] private Material _shadowMaterial;
        [Tooltip("機種名・リールの文字のフォント（♦♥などの記号が出せるもの。紙幣と同じ LiberationSans SDF - Fallback）。")]
        [SerializeField] private TMP_FontAsset _textFont;

        [Header("配置（ワールド座標。テーブルは約5倍の大きさで作られているので、部屋の物も同じ尺度）")]
        [Tooltip("床の高さ（テーブルの脚の下端）。")]
        [SerializeField] private float _floorY = -2.33f;
        [Tooltip("カーペットの大きさ（X, Z）。どの解像度・カメラ位置でも端が見えないよう広くとる（遠くは暗闇に溶ける）。")]
        [SerializeField] private Vector2 _carpetSize = new Vector2(160f, 100f);
        [Tooltip("カーペットの中心のZ。")]
        [SerializeField] private float _carpetCenterZ = -15f;
        [Tooltip("奥の壁のZ。")]
        [SerializeField] private float _wallZ = -40f;
        [SerializeField] private float _wallWidth = 150f;
        [SerializeField] private float _wallHeight = 30f;
        [Tooltip("壁の金の装飾ラインの高さ（床から）。")]
        [SerializeField] private float[] _wallLineHeights = { 9.4f, 9.9f };
        [SerializeField] private float _wallLineThickness = 0.2f;

        [Header("テーブルの周りの床")]
        [Tooltip("テーブルの中心（X, Z）。真下の影の中心。")]
        [SerializeField] private Vector2 _tableCenter = new Vector2(0f, 0.5f);
        [Tooltip("光だまりの中心（X, Z）。カメラからはテーブルの奥の床がよく見えるので、テーブルより少し奥に寄せる。")]
        [SerializeField] private Vector2 _floorPoolCenter = new Vector2(0f, -5f);
        [Tooltip("テーブルの光が周りの床にこぼれた、ぼんやり明るい楕円の大きさ（X, Z）。")]
        [SerializeField] private Vector2 _floorPoolSize = new Vector2(44f, 42f);
        [Tooltip("光だまりの色と濃さ（アルファ）。")]
        [SerializeField] private Color _floorPoolColor = new Color(1f, 0.55f, 0.40f, 0.34f);
        [Tooltip("テーブルの真下の、ぼかした影の大きさ（X, Z）。")]
        [SerializeField] private Vector2 _tableShadowSize = new Vector2(11.5f, 9f);
        [Tooltip("影の濃さ。")]
        [Range(0f, 1f)] [SerializeField] private float _tableShadowAlpha = 0.85f;

        [Header("スロットマシン")]
        [Tooltip("島ごとの位置・奥行き・向き・台数。テーブルの真後ろ（画面上部の中央）は空けておく。")]
        [SerializeField] private SlotIsland[] _slotIslands =
        {
            new SlotIsland { X = -35f, Z = -27.5f, Yaw = 15f, Count = 3 },
            new SlotIsland { X = -22.5f, Z = -32f, Yaw = 7f, Count = 3 },
            new SlotIsland { X = -12.5f, Z = -35.5f, Yaw = -3f, Count = 2 },
            new SlotIsland { X = 13.5f, Z = -34.5f, Yaw = 4f, Count = 2 },
            new SlotIsland { X = 24f, Z = -30.5f, Yaw = -9f, Count = 3 },
            new SlotIsland { X = 36f, Z = -26.5f, Yaw = -16f, Count = 3 },
        };
        [Tooltip("本体の幅・高さ・奥行き。")]
        [SerializeField] private Vector3 _slotBodySize = new Vector3(3.4f, 6.4f, 2.6f);
        [Tooltip("上部の看板（機種名のパネル）の高さ。")]
        [SerializeField] private float _slotMarqueeHeight = 1.5f;
        [Tooltip("機種名の一覧。台ごとにランダムに（隣の台と同じ名前が続かないように）割り当てる。")]
        [SerializeField] private string[] _machineNames =
        {
            "LUCKY 7", "GOLDEN BAR", "JACKPOT", "TRASH KING", "DIAMOND RUSH", "MEGA CHERRY", "BIG BONUS", "ROYAL FLUSH"
        };
        [Tooltip("リールの絵柄の一覧（画面に3つ横に並べる）。")]
        [SerializeField] private ReelSymbol[] _reelSymbols =
        {
            new ReelSymbol { Text = "7", Color = new Color(1f, 0.30f, 0.20f) },
            new ReelSymbol { Text = "BAR", Color = new Color(1f, 0.74f, 0.28f) },
            new ReelSymbol { Text = "♦", Color = new Color(1f, 0.30f, 0.20f) },
            new ReelSymbol { Text = "♥", Color = new Color(1f, 0.30f, 0.20f) },
            new ReelSymbol { Text = "♠", Color = new Color(1f, 0.90f, 0.72f) },
            new ReelSymbol { Text = "♣", Color = new Color(1f, 0.90f, 0.72f) },
        };
        [Tooltip("機種名の文字の色（暖かい白）。")]
        [SerializeField] private Color _nameTextColor = new Color(1f, 0.92f, 0.74f);
        [Tooltip("看板・画面の地（文字の後ろ）の明るさ。ランプの色にこの倍率を掛ける（文字が読めるよう暗めに）。")]
        [Range(0f, 1f)] [SerializeField] private float _panelBackingBrightness = 0.2f;
        [Tooltip("筐体の縁取り（正面の左右の縦の辺・看板の縁・足元の線）の明るさ。画面や機種名より目立たない程度に。")]
        [Range(0f, 1f)] [SerializeField] private float _outlineBrightness = 0.4f;
        [Tooltip("筐体の縁取りの線の太さ。")]
        [SerializeField] private float _outlineThickness = 0.07f;
        [Tooltip("操作台の小さなボタンの明るさ。")]
        [Range(0f, 1f)] [SerializeField] private float _buttonBrightness = 0.8f;
        [Tooltip("スツールを置かない台の間隔（3なら3台に1台は置かない。0で全部に置く）。")]
        [SerializeField] private int _stoolSkipEvery = 3;

        [Header("照明")]
        [Tooltip("背景の物を置く Rendering Layer の番号。背景用のポイントライトだけがこのレイヤーを照らす（テーブルは Default のまま）。")]
        [Range(0, 31)] [SerializeField] private int _roomRenderingLayer = 1;

        [Header("別のテーブル（ブラックジャック）")]
        [SerializeField] private OtherTable[] _otherTables =
        {
            new OtherTable { Position = new Vector2(-15.5f, -19f), Yaw = 18f },
            new OtherTable { Position = new Vector2(16.5f, -20.5f), Yaw = -15f },
        };
        [Tooltip("半円形の天板の半径（横、奥行き）。")]
        [SerializeField] private Vector2 _otherTableRadius = new Vector2(3.6f, 2.7f);
        [Tooltip("天板の高さ（ワールドY）。")]
        [SerializeField] private float _otherTableTopY = 1.2f;
        [Tooltip("テーブルの周りの椅子の数。")]
        [Range(0, 4)] [SerializeField] private int _chairsPerTable = 3;

        [Header("色")]
        [SerializeField] private Color _wallColor = new Color(0.06f, 0.038f, 0.027f);
        [SerializeField] private Color _wallLineColor = new Color(0.46f, 0.34f, 0.12f);
        [SerializeField] private Color _slotFrontColor = new Color(0.10f, 0.065f, 0.05f);
        [SerializeField] private Color _slotSideColor = new Color(0.055f, 0.036f, 0.03f);
        [SerializeField] private Color _slotTopColor = new Color(0.13f, 0.085f, 0.06f);
        [Tooltip("スロットマシンの画面の色（暖かい白）。")]
        [SerializeField] private Color _lampScreenColor = new Color(1f, 0.86f, 0.62f);
        [Tooltip("看板の縁の色（金）。")]
        [SerializeField] private Color _lampGoldColor = new Color(1f, 0.70f, 0.24f);
        [Tooltip("看板の縁の色（赤）。")]
        [SerializeField] private Color _lampRedColor = new Color(0.95f, 0.22f, 0.14f);
        [SerializeField] private Color _otherTableFeltColor = new Color(0.07f, 0.24f, 0.15f);
        [Tooltip("天板の縁（黒に近い濃い茶）。")]
        [SerializeField] private Color _otherTableRimColor = new Color(0.075f, 0.045f, 0.03f);
        [Tooltip("天板の下の台（脚）の色。床より一段明るい焦げ茶。")]
        [SerializeField] private Color _otherTableBaseColor = new Color(0.14f, 0.085f, 0.055f);
        [Tooltip("台の上下の縁の、細い金の帯の色。")]
        [SerializeField] private Color _otherTableTrimColor = new Color(0.42f, 0.31f, 0.11f);
        [Tooltip("台の根元の床に、天板の光がこぼれたような弱い光だまりの色と濃さ。")]
        [SerializeField] private Color _otherTableFloorPoolColor = new Color(1f, 0.7f, 0.45f, 0.22f);
        [Tooltip("椅子の座面（暗い赤の革）。")]
        [SerializeField] private Color _chairSeatColor = new Color(0.11f, 0.03f, 0.03f);
        [SerializeField] private Color _chairFrameColor = new Color(0.05f, 0.035f, 0.03f);
        [Tooltip("スロットマシンの操作台の色。")]
        [SerializeField] private Color _slotPanelColor = new Color(0.12f, 0.08f, 0.06f);
        [Tooltip("天板の上のカードの色（暗いところに置かれているので少し落とした白）。")]
        [SerializeField] private Color _cardColor = new Color(0.62f, 0.60f, 0.55f);
        [SerializeField] private Color _chipRedColor = new Color(0.45f, 0.08f, 0.07f);
        [SerializeField] private Color _chipGoldColor = new Color(0.55f, 0.42f, 0.14f);
        [Tooltip("別のテーブルの天板を照らす楕円の色。")]
        [SerializeField] private Color _otherTableSpotColor = new Color(1f, 0.85f, 0.55f, 0.75f);

        [Header("光の動き")]
        [Tooltip("普段の光る部分の明るさ（0〜1）。")]
        [Range(0f, 1f)] [SerializeField] private float _idleBrightness = 0.55f;
        [Tooltip("普段の明るさのゆらぎの幅（±）。目立たない程度に。")]
        [Range(0f, 0.5f)] [SerializeField] private float _idleVariation = 0.08f;
        [Tooltip("明るさが1往復する時間の範囲（秒）。台ごとにこの範囲でばらばらに決まる。")]
        [SerializeField] private Vector2 _idlePeriodRange = new Vector2(2.5f, 5.5f);
        [Tooltip("ランプのにじみ（光の輪）の普段の濃さ。")]
        [Range(0f, 1f)] [SerializeField] private float _haloStrength = 0.16f;
        [Tooltip("あがりのとき、一斉に光る強さ（0〜1。1で下の色まで明るくなる）。")]
        [Range(0f, 1f)] [SerializeField] private float _jackpotStrength = 1f;
        [Tooltip("あがりのとき、一斉に光る色。")]
        [SerializeField] private Color _jackpotColor = new Color(1f, 0.95f, 0.78f);
        [Tooltip("あがりのとき、ランプのにじみがどれだけ濃くなるか。")]
        [Range(0f, 1f)] [SerializeField] private float _jackpotHaloStrength = 0.7f;
        [Tooltip("あがりのとき、一斉に光ってから普段の明るさに戻るまでの時間（秒）。")]
        [SerializeField] private float _jackpotSeconds = 1.0f;

        // 光る部分（頂点の範囲）と、それがどの台のものか。
        private struct LightPart
        {
            public int Start;
            public int Count;
            public Color BaseColor;
            public int Machine;
            public float Scale;
        }

        // 機種名・リールの文字（頂点カラーを毎フレーム書き換えて、明るさを光る部分と合わせる）。
        private class MachineText
        {
            public TextMeshPro Text;
            public int Machine;
            public Color32[] BaseColors;
            public float DistanceDim;
        }

        private readonly List<GameObject> _generated = new List<GameObject>();
        private readonly List<Mesh> _meshes = new List<Mesh>();
        private readonly List<LightPart> _lampParts = new List<LightPart>();
        private readonly List<LightPart> _glowParts = new List<LightPart>();
        private readonly List<MachineText> _texts = new List<MachineText>();
        private Mesh _lampMesh;
        private Mesh _glowMesh;
        private Color[] _lampColors;
        private Color[] _glowColors;
        private float[] _machinePhase;
        private float[] _machinePeriod;
        private int _machineCount;
        private float _jackpotStartTime = -100f;
        private int _floorPoolPart = -1;
        private bool _dirty;

        /// <summary>テーブル周りの床の光だまりの色と濃さを変える（照明の案に合わせる）。</summary>
        public void SetFloorPoolColor(Color color)
        {
            _floorPoolColor = color;
            if (_floorPoolPart >= 0 && _floorPoolPart < _glowParts.Count)
            {
                var part = _glowParts[_floorPoolPart];
                part.BaseColor = color;
                _glowParts[_floorPoolPart] = part;
                UpdateLights(Application.isPlaying ? Time.time : 0f);
            }
        }

        /// <summary>誰かがあがったとき：背景のスロットマシンの光と文字を一斉にパッと明るくし、少しかけて普段の明るさへ戻す。</summary>
        public void PlayJackpot()
        {
            _jackpotStartTime = Time.time;
        }

        private void OnEnable()
        {
            Build();
        }

        private void OnDisable()
        {
            Clear();
        }

        private void OnValidate()
        {
            // OnValidate の中では作り直せないので、次の Update で作り直す。
            _dirty = true;
        }

        private void Update()
        {
            if (_dirty)
            {
                _dirty = false;
                Build();
            }
            UpdateLights(Application.isPlaying ? Time.time : 0f);
        }

        private void Build()
        {
            Clear();

            var room = new MeshBuilder();
            var lamps = new MeshBuilder();
            var glows = new MeshBuilder();
            var shadows = new MeshBuilder();
            _lampParts.Clear();
            _glowParts.Clear();
            _texts.Clear();

            // 台ごとの明るさのゆらぎと、機種名・リールの割り当て（毎回同じになるよう固定の乱数で決める）。
            var random = new System.Random(20261017);
            _machineCount = 0;
            foreach (var island in _slotIslands)
            {
                _machineCount += Mathf.Max(0, island.Count);
            }
            _machinePhase = new float[Mathf.Max(1, _machineCount)];
            _machinePeriod = new float[_machinePhase.Length];
            for (int i = 0; i < _machinePhase.Length; i++)
            {
                _machinePhase[i] = (float)random.NextDouble() * Mathf.PI * 2f;
                _machinePeriod[i] = Mathf.Lerp(_idlePeriodRange.x, _idlePeriodRange.y, (float)random.NextDouble());
            }

            BuildWall(room);
            BuildFloorAroundTable(glows, shadows);
            BuildSlotMachines(room, lamps, glows, random);
            BuildOtherTables(room, glows, shadows);

            var carpet = new MeshBuilder();
            float hx = _carpetSize.x / 2f;
            float hz = _carpetSize.y / 2f;
            carpet.AddQuad(new Vector3(-hx, _floorY, _carpetCenterZ - hz), new Vector3(-hx, _floorY, _carpetCenterZ + hz),
                new Vector3(hx, _floorY, _carpetCenterZ + hz), new Vector3(hx, _floorY, _carpetCenterZ - hz), Color.white);

            CreateRenderer("CasinoCarpet", carpet, _carpetMaterial);
            CreateRenderer("CasinoRoom", room, _roomMaterial);
            _lampMesh = CreateRenderer("CasinoLamps", lamps, _lampMaterial);
            _glowMesh = CreateRenderer("CasinoGlows", glows, _glowMaterial);
            CreateRenderer("CasinoShadows", shadows, _shadowMaterial);
            _lampColors = lamps.Colors.ToArray();
            _glowColors = glows.Colors.ToArray();

            UpdateLights(0f);
        }

        private void Clear()
        {
            foreach (var go in _generated)
            {
                if (go != null)
                {
                    DestroyGenerated(go);
                }
            }
            foreach (var mesh in _meshes)
            {
                if (mesh != null)
                {
                    DestroyGenerated(mesh);
                }
            }
            _generated.Clear();
            _meshes.Clear();
            _texts.Clear();
            _lampMesh = null;
            _glowMesh = null;
        }

        private static void DestroyGenerated(UnityEngine.Object target)
        {
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private Mesh CreateRenderer(string name, MeshBuilder builder, Material material)
        {
            var go = new GameObject(name);
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetParent(transform, false);
            var mesh = builder.ToMesh(name);
            mesh.hideFlags = HideFlags.DontSave;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.renderingLayerMask = 1u << _roomRenderingLayer;
            _generated.Add(go);
            _meshes.Add(mesh);
            return mesh;
        }

        private void BuildWall(MeshBuilder room)
        {
            float hw = _wallWidth / 2f;
            room.AddQuad(new Vector3(-hw, _floorY, _wallZ), new Vector3(-hw, _floorY + _wallHeight, _wallZ),
                new Vector3(hw, _floorY + _wallHeight, _wallZ), new Vector3(hw, _floorY, _wallZ), _wallColor);
            foreach (float h in _wallLineHeights)
            {
                float y = _floorY + h;
                float z = _wallZ + 0.03f;
                room.AddQuad(new Vector3(-hw, y - _wallLineThickness / 2f, z), new Vector3(-hw, y + _wallLineThickness / 2f, z),
                    new Vector3(hw, y + _wallLineThickness / 2f, z), new Vector3(hw, y - _wallLineThickness / 2f, z), _wallLineColor);
            }
        }

        /// <summary>テーブルの光が周りの床にこぼれた光だまり（加算）と、テーブルの真下のぼかした影（半透明の黒）。</summary>
        private void BuildFloorAroundTable(MeshBuilder glows, MeshBuilder shadows)
        {
            _floorPoolPart = _glowParts.Count;
            AddFlatGlow(glows, new Vector3(_floorPoolCenter.x, _floorY + 0.01f, _floorPoolCenter.y), _floorPoolSize, _floorPoolColor);
            shadows.AddFlatQuad(new Vector3(_tableCenter.x, _floorY + 0.02f, _tableCenter.y), _tableShadowSize,
                new Color(0f, 0f, 0f, _tableShadowAlpha));
        }

        private void AddFlatGlow(MeshBuilder glows, Vector3 center, Vector2 size, Color color)
        {
            int start = glows.VertexCount;
            glows.AddFlatQuad(center, size, color);
            _glowParts.Add(new LightPart { Start = start, Count = glows.VertexCount - start, BaseColor = color, Machine = -1 });
        }

        private void BuildSlotMachines(MeshBuilder room, MeshBuilder lamps, MeshBuilder glows, System.Random random)
        {
            Vector3 body = _slotBodySize;
            float pitch = body.x * 1.08f + 0.12f;
            int machine = 0;
            int lastName = -1;
            foreach (var island in _slotIslands)
            {
                var islandMatrix = Matrix4x4.TRS(new Vector3(island.X, _floorY, island.Z), Quaternion.Euler(0f, island.Yaw, 0f), Vector3.one);
                for (int k = 0; k < island.Count; k++)
                {
                    float localX = (k - (island.Count - 1) / 2f) * pitch;
                    var matrix = islandMatrix * Matrix4x4.Translate(new Vector3(localX, 0f, 0f));
                    room.Matrix = matrix;
                    lamps.Matrix = matrix;
                    glows.Matrix = matrix;

                    // ここからは台のローカル座標（底面の正面中央が原点、正面は +Z、上が +Y）。
                    float zFront = 0f;
                    float zBack = -body.z;
                    room.AddBox(new Vector3(0f, 0f, zBack), new Vector3(0f, body.y, zFront), body.x, _slotFrontColor, _slotSideColor, _slotTopColor);
                    float marqueeBottom = body.y;
                    float marqueeTop = marqueeBottom + _slotMarqueeHeight;
                    room.AddBox(new Vector3(0f, marqueeBottom, zBack + 0.2f), new Vector3(0f, marqueeTop, zFront + 0.15f), body.x * 1.08f,
                        _slotFrontColor, _slotSideColor, _slotTopColor);

                    Color frame = machine % 2 == 0 ? _lampGoldColor : _lampRedColor;
                    float zMarquee = zFront + 0.17f;
                    // 看板：暗い地＋光る縁取り（機種名の文字を読みやすく）。
                    float mw = body.x * 0.98f;
                    float mb = marqueeBottom + 0.1f;
                    float mt = marqueeTop - 0.1f;
                    AddLamp(lamps, machine, frame, 0f, mb, mt, mw, zMarquee, _panelBackingBrightness);
                    AddFrame(lamps, machine, frame, mb, mt, mw, zMarquee + 0.01f, 0.09f);
                    // 画面：暗い地＋暖かい白の縁取り（リールの絵柄を読みやすく）。
                    float sb = body.y * 0.50f;
                    float st = body.y * 0.80f;
                    float sw = body.x * 0.80f;
                    AddLamp(lamps, machine, _lampScreenColor, 0f, sb, st, sw, zFront + 0.02f, _panelBackingBrightness);
                    AddFrame(lamps, machine, _lampScreenColor, sb, st, sw, zFront + 0.03f, 0.08f);
                    // 画面の下の操作台（手前に少し張り出した台）と、その正面の小さな光るボタン。
                    float panelTop = sb - 0.25f;
                    float panelBottom = panelTop - 0.5f;
                    float panelFront = zFront + 0.75f;
                    room.AddBox(new Vector3(0f, panelBottom, zFront - 0.1f), new Vector3(0f, panelTop, panelFront), body.x * 0.92f,
                        _slotPanelColor, _slotSideColor, _slotPanelColor * 1.35f);
                    Color[] buttons = { _lampRedColor, _lampGoldColor, _lampGoldColor, _lampScreenColor };
                    for (int b = 0; b < buttons.Length; b++)
                    {
                        float bx = (b - (buttons.Length - 1) / 2f) * body.x * 0.2f;
                        AddLamp(lamps, machine, buttons[b], bx, panelBottom + 0.16f, panelTop - 0.14f, body.x * 0.12f, panelFront + 0.02f, _buttonBrightness);
                    }

                    // 筐体の縁取り（正面の左右の縦の辺、足元の線、看板の箱の縁）。暗闇でも形がぼんやり分かるように。
                    float edgeZ = zFront + 0.02f;
                    float halfBody = body.x / 2f;
                    float t = _outlineThickness;
                    AddLamp(lamps, machine, frame, -halfBody + t / 2f, 0f, body.y, t, edgeZ, _outlineBrightness);
                    AddLamp(lamps, machine, frame, halfBody - t / 2f, 0f, body.y, t, edgeZ, _outlineBrightness);
                    AddLamp(lamps, machine, frame, 0f, 0.05f, 0.05f + t, body.x, edgeZ, _outlineBrightness);
                    float halfMarquee = body.x * 1.08f / 2f;
                    float marqueeEdgeZ = zFront + 0.16f;
                    AddLamp(lamps, machine, frame, -halfMarquee + t / 2f, marqueeBottom, marqueeTop, t, marqueeEdgeZ, _outlineBrightness);
                    AddLamp(lamps, machine, frame, halfMarquee - t / 2f, marqueeBottom, marqueeTop, t, marqueeEdgeZ, _outlineBrightness);
                    AddLamp(lamps, machine, frame, 0f, marqueeTop - t, marqueeTop, body.x * 1.08f, marqueeEdgeZ, _outlineBrightness);

                    // 台の前の丸いスツール（何台かに1台は置かない）。
                    if (_stoolSkipEvery <= 0 || machine % _stoolSkipEvery != _stoolSkipEvery - 1)
                    {
                        var stool = new Vector3(0f, 0f, zFront + 2.1f);
                        room.AddEllipsePrism(stool, 0.42f, 0.42f, 0.12f, 10, _chairFrameColor, _chairFrameColor * 1.3f, 0f, Mathf.PI * 2f);
                        room.AddEllipsePrism(stool, 0.1f, 0.1f, 2.0f, 6, _chairFrameColor, _chairFrameColor, 0f, Mathf.PI * 2f);
                        room.AddEllipsePrism(stool + Vector3.up * 2.0f, 0.55f, 0.55f, 0.22f, 12, _chairSeatColor, _chairSeatColor * 1.3f, 0f, Mathf.PI * 2f);
                    }

                    // ランプのにじみ（看板と画面の周りに、加算の光の輪）。
                    AddHalo(glows, machine, frame, (marqueeBottom + marqueeTop) / 2f, zMarquee + 0.05f, body.x * 1.9f, _slotMarqueeHeight * 2.6f);
                    AddHalo(glows, machine, _lampScreenColor, (sb + st) / 2f, zFront + 0.06f, body.x * 1.4f, (st - sb) * 1.9f);

                    // 機種名（隣の台と同じ名前が続かないように）と、リールの絵柄3つ。
                    int nameIndex = PickName(random, lastName);
                    lastName = nameIndex;
                    string name = nameIndex >= 0 ? _machineNames[nameIndex] : string.Empty;
                    AddText(matrix, machine, name, _nameTextColor, new Vector3(0f, (mb + mt) / 2f, zMarquee + 0.04f), new Vector2(mw * 0.88f, (mt - mb) * 0.8f));
                    AddText(matrix, machine, BuildReels(random), Color.white, new Vector3(0f, (sb + st) / 2f, zFront + 0.05f), new Vector2(sw * 0.86f, (st - sb) * 0.62f));

                    machine++;
                }
            }
            room.Matrix = Matrix4x4.identity;
            lamps.Matrix = Matrix4x4.identity;
            glows.Matrix = Matrix4x4.identity;
        }

        private int PickName(System.Random random, int previous)
        {
            if (_machineNames == null || _machineNames.Length == 0)
            {
                return -1;
            }
            if (_machineNames.Length == 1)
            {
                return 0;
            }
            int index = random.Next(_machineNames.Length - 1);
            // 直前の台の名前を飛ばして選ぶ（同じ名前が隣に続かない）。
            if (previous >= 0 && index >= previous)
            {
                index++;
            }
            return index;
        }

        private string BuildReels(System.Random random)
        {
            if (_reelSymbols == null || _reelSymbols.Length == 0)
            {
                return string.Empty;
            }
            var parts = new string[3];
            for (int i = 0; i < 3; i++)
            {
                var symbol = _reelSymbols[random.Next(_reelSymbols.Length)];
                parts[i] = $"<color=#{ColorUtility.ToHtmlStringRGB(symbol.Color)}>{symbol.Text}</color>";
            }
            return string.Join("  ", parts);
        }

        private void AddText(Matrix4x4 machineMatrix, int machine, string text, Color color, Vector3 localPosition, Vector2 size)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            var go = new GameObject("SlotText");
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetParent(transform, false);
            go.transform.position = machineMatrix.MultiplyPoint3x4(localPosition);
            // TextMeshPro の文字は -Z 側から読めるので、台の正面（+Z）から読めるよう180度回す。
            go.transform.rotation = machineMatrix.rotation * Quaternion.Euler(0f, 180f, 0f);
            var tmp = go.AddComponent<TextMeshPro>();
            if (_textFont != null)
            {
                tmp.font = _textFont;
            }
            tmp.rectTransform.sizeDelta = size;
            tmp.text = text;
            tmp.richText = true;
            tmp.fontStyle = FontStyles.Bold;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.5f;
            tmp.fontSizeMax = 40f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.color = color;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            tmp.ForceMeshUpdate();

            var colors = tmp.mesh.colors32;
            float d = Vector2.Distance(new Vector2(go.transform.position.x, go.transform.position.z), _tableCenter);
            _texts.Add(new MachineText
            {
                Text = tmp,
                Machine = machine,
                BaseColors = (Color32[])colors.Clone(),
                // 部屋の物と同じように、遠い文字ほど暗くする（38〜75で暗闇に溶ける）。
                DistanceDim = 1f - Mathf.Clamp01((d - 38f) / 37f)
            });
            _generated.Add(go);
        }

        private void AddLamp(MeshBuilder lamps, int machine, Color color, float x, float bottom, float top, float width, float z, float scale)
        {
            int start = lamps.VertexCount;
            float hw = width / 2f;
            lamps.AddQuad(new Vector3(x - hw, bottom, z), new Vector3(x - hw, top, z), new Vector3(x + hw, top, z), new Vector3(x + hw, bottom, z), color);
            _lampParts.Add(new LightPart { Start = start, Count = lamps.VertexCount - start, BaseColor = color, Machine = machine, Scale = scale });
        }

        /// <summary>パネルの縁取り（4本の細い光る帯）。</summary>
        private void AddFrame(MeshBuilder lamps, int machine, Color color, float bottom, float top, float width, float z, float thickness)
        {
            float hw = width / 2f;
            AddLamp(lamps, machine, color, 0f, top - thickness, top, width, z, 1f);
            AddLamp(lamps, machine, color, 0f, bottom, bottom + thickness, width, z, 1f);
            AddLamp(lamps, machine, color, -hw + thickness / 2f, bottom, top, thickness, z, 1f);
            AddLamp(lamps, machine, color, hw - thickness / 2f, bottom, top, thickness, z, 1f);
        }

        private void AddHalo(MeshBuilder glows, int machine, Color color, float centerY, float z, float width, float height)
        {
            int start = glows.VertexCount;
            float hw = width / 2f;
            float hh = height / 2f;
            glows.AddQuad(new Vector3(-hw, centerY - hh, z), new Vector3(-hw, centerY + hh, z),
                new Vector3(hw, centerY + hh, z), new Vector3(hw, centerY - hh, z), color);
            _glowParts.Add(new LightPart { Start = start, Count = glows.VertexCount - start, BaseColor = color, Machine = machine, Scale = 1f });
        }

        /// <summary>
        /// ブラックジャック用の半円形のテーブル（緑のフェルトの天板、濃い茶の縁、下の台、周りの椅子、天板の上のカードとチップ）。
        /// ローカル座標では丸い側（プレイヤー側）が +Z。
        /// </summary>
        private void BuildOtherTables(MeshBuilder room, MeshBuilder glows, MeshBuilder shadows)
        {
            const int segments = 20;
            float rx = _otherTableRadius.x;
            float rz = _otherTableRadius.y;
            float top = _otherTableTopY;
            float rim = 0.4f;
            foreach (var table in _otherTables)
            {
                var matrix = Matrix4x4.TRS(new Vector3(table.Position.x, 0f, table.Position.y), Quaternion.Euler(0f, table.Yaw, 0f), Vector3.one);
                room.Matrix = matrix;
                glows.Matrix = matrix;
                shadows.Matrix = matrix;

                // 根元の床の、弱い光だまり（天板の光がこぼれたように）とぼかした影（テーブルが床に立って見えるように）。
                int poolStart = glows.VertexCount;
                glows.AddFlatQuad(new Vector3(0f, _floorY + 0.01f, rz * 0.3f), new Vector2(rx * 3.4f, rz * 3.0f), _otherTableFloorPoolColor);
                _glowParts.Add(new LightPart { Start = poolStart, Count = glows.VertexCount - poolStart, BaseColor = _otherTableFloorPoolColor, Machine = -1 });
                shadows.AddFlatQuad(new Vector3(0f, _floorY + 0.02f, rz * 0.2f), new Vector2(rx * 2.0f, rz * 1.7f), new Color(0f, 0f, 0f, 0.7f));

                // 下の台（天板より一回り小さい半円の柱。床より一段明るい焦げ茶）と、上下の縁の細い金の帯。
                float baseHeight = top - 0.45f - _floorY;
                room.AddEllipsePrism(new Vector3(0f, _floorY, 0f), rx * 0.7f, rz * 0.6f, baseHeight, segments, _otherTableBaseColor, _otherTableBaseColor, 0f, Mathf.PI);
                room.AddEllipsePrism(new Vector3(0f, top - 0.6f, 0f), rx * 0.72f, rz * 0.62f, 0.1f, segments, _otherTableTrimColor, _otherTableTrimColor, 0f, Mathf.PI);
                room.AddEllipsePrism(new Vector3(0f, _floorY, 0f), rx * 0.72f, rz * 0.62f, 0.1f, segments, _otherTableTrimColor, _otherTableTrimColor, 0f, Mathf.PI);
                // 天板の縁（帯と上面）とフェルト。
                room.AddEllipsePrism(new Vector3(0f, top - 0.45f, 0f), rx + rim, rz + rim, 0.5f, segments, _otherTableRimColor, _otherTableRimColor * 1.25f, 0f, Mathf.PI);
                room.AddEllipseDisc(new Vector3(0f, top + 0.06f, 0f), rx, rz, segments, _otherTableFeltColor, 0f, Mathf.PI);

                // 天板の上の小さな光だまり。
                int start = glows.VertexCount;
                glows.AddFlatQuad(new Vector3(0f, top + 0.08f, rz * 0.4f), new Vector2(rx * 1.7f, rz * 1.3f), _otherTableSpotColor);
                _glowParts.Add(new LightPart { Start = start, Count = glows.VertexCount - start, BaseColor = _otherTableSpotColor, Machine = -1 });

                // 天板の上のカード（プレイヤーの前に数枚）と、チップの小さな山。
                for (int c = 0; c < 4; c++)
                {
                    float a = Mathf.Lerp(0.35f, Mathf.PI - 0.35f, c / 3f);
                    var p = new Vector3(Mathf.Cos(a) * rx * 0.62f, top + 0.09f, Mathf.Sin(a) * rz * 0.62f);
                    room.AddFlatQuad(p, new Vector2(0.32f, 0.46f), _cardColor);
                    room.AddFlatQuad(p + new Vector3(0.16f, 0.005f, 0.05f), new Vector2(0.32f, 0.46f), _cardColor * 0.92f);
                }
                room.AddEllipsePrism(new Vector3(-0.6f, top + 0.06f, 0.3f), 0.12f, 0.12f, 0.2f, 8, _chipRedColor, _chipRedColor * 1.2f, 0f, Mathf.PI * 2f);
                room.AddEllipsePrism(new Vector3(-0.3f, top + 0.06f, 0.35f), 0.12f, 0.12f, 0.14f, 8, _chipGoldColor, _chipGoldColor * 1.2f, 0f, Mathf.PI * 2f);
                room.AddEllipsePrism(new Vector3(0.05f, top + 0.06f, 0.3f), 0.12f, 0.12f, 0.26f, 8, _chipRedColor, _chipRedColor * 1.2f, 0f, Mathf.PI * 2f);

                // 椅子（丸い側に並べる。脚＋座面＋背もたれ）。
                for (int i = 0; i < _chairsPerTable; i++)
                {
                    float a = _chairsPerTable == 1 ? Mathf.PI / 2f : Mathf.Lerp(0.45f, Mathf.PI - 0.45f, i / (float)(_chairsPerTable - 1));
                    var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    var seat = new Vector3(dir.x * (rx + 1.3f), 0f, dir.z * (rz + 1.3f));
                    float seatY = _floorY + 2.2f;
                    room.AddEllipsePrism(new Vector3(seat.x, _floorY, seat.z), 0.12f, 0.12f, seatY - 0.2f - _floorY, 6, _chairFrameColor, _chairFrameColor, 0f, Mathf.PI * 2f);
                    room.AddEllipsePrism(new Vector3(seat.x, seatY - 0.2f, seat.z), 0.55f, 0.55f, 0.22f, 12, _chairSeatColor, _chairSeatColor * 1.25f, 0f, Mathf.PI * 2f);
                    // 背もたれ（テーブルと反対側に立てる）。
                    var back = seat + dir * 0.55f;
                    var side = new Vector3(-dir.z, 0f, dir.x) * 0.5f;
                    var b0 = new Vector3(back.x, seatY, back.z);
                    room.AddQuad(b0 - side, b0 - side + Vector3.up * 1.2f, b0 + side + Vector3.up * 1.2f, b0 + side, _chairSeatColor * 0.85f);
                }
            }
            room.Matrix = Matrix4x4.identity;
            glows.Matrix = Matrix4x4.identity;
            shadows.Matrix = Matrix4x4.identity;
        }

        private void UpdateLights(float time)
        {
            if (_lampMesh == null || _glowMesh == null || _machinePhase == null)
            {
                return;
            }

            float jackpot = 0f;
            if (Application.isPlaying && _jackpotSeconds > 0f)
            {
                float e = (time - _jackpotStartTime) / _jackpotSeconds;
                if (e >= 0f && e < 1f)
                {
                    jackpot = (1f - e) * (1f - e) * _jackpotStrength;
                }
            }

            foreach (var part in _lampParts)
            {
                float b = MachineBrightness(part.Machine, time);
                Color c = Color.Lerp(part.BaseColor * b * part.Scale, _jackpotColor, jackpot * (part.Scale < 1f ? 0.5f : 1f));
                c.a = 1f;
                c = ToVertexColor(c);
                for (int i = 0; i < part.Count; i++)
                {
                    _lampColors[part.Start + i] = c;
                }
            }
            _lampMesh.colors = _lampColors;

            foreach (var part in _glowParts)
            {
                Color c;
                if (part.Machine < 0)
                {
                    c = part.BaseColor;
                }
                else
                {
                    float b = MachineBrightness(part.Machine, time);
                    c = Color.Lerp(part.BaseColor, _jackpotColor, jackpot);
                    c.a = _haloStrength * b + _jackpotHaloStrength * jackpot;
                }
                c = ToVertexColor(c);
                for (int i = 0; i < part.Count; i++)
                {
                    _glowColors[part.Start + i] = c;
                }
            }
            _glowMesh.colors = _glowColors;

            UpdateTexts(time, jackpot);
        }

        /// <summary>機種名・リールの文字の明るさを、台の光る部分と同じようにゆらし、あがりのときは一斉に明るくする。</summary>
        private void UpdateTexts(float time, float jackpot)
        {
            Color32 jackpotColor = _jackpotColor;
            foreach (var text in _texts)
            {
                if (text.Text == null || text.BaseColors == null)
                {
                    continue;
                }
                var meshInfo = text.Text.textInfo.meshInfo;
                if (meshInfo == null || meshInfo.Length == 0 || meshInfo[0].colors32 == null || meshInfo[0].colors32.Length < text.BaseColors.Length)
                {
                    continue;
                }
                // 文字は普段から読めるよう、光る部分より明るめ（明るさ0.55で元の色）。
                float b = Mathf.Clamp01(MachineBrightness(text.Machine, time) / 0.55f) * text.DistanceDim;
                var colors = meshInfo[0].colors32;
                for (int i = 0; i < text.BaseColors.Length; i++)
                {
                    var baseColor = text.BaseColors[i];
                    var dimmed = new Color32((byte)(baseColor.r * b), (byte)(baseColor.g * b), (byte)(baseColor.b * b), baseColor.a);
                    colors[i] = Color32.Lerp(dimmed, new Color32(jackpotColor.r, jackpotColor.g, jackpotColor.b, baseColor.a), jackpot);
                }
                text.Text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
            }
        }

        private float MachineBrightness(int machine, float time)
        {
            if (machine < 0 || machine >= _machinePhase.Length)
            {
                return _idleBrightness;
            }
            float period = Mathf.Max(0.1f, _machinePeriod[machine]);
            return Mathf.Clamp01(_idleBrightness + _idleVariation * Mathf.Sin(time * Mathf.PI * 2f / period + _machinePhase[machine]));
        }

        /// <summary>
        /// 頂点カラーは色空間の変換を受けない（マテリアルの色と違い、Linear のプロジェクトでもそのままの値で描かれる）ので、
        /// Inspector で指定した色（sRGB）と同じ見た目になるよう、Linear のときは変換してから頂点に入れる。
        /// </summary>
        private static Color ToVertexColor(Color color)
        {
            return QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color;
        }

        /// <summary>
        /// 頂点カラー付きの簡単な形（四角・箱・楕円の柱・楕円の板）を1つのメッシュに積み上げる。
        /// <see cref="Matrix"/> を設定すると、その座標系（台やテーブルのローカル座標）で形を置ける。
        /// </summary>
        private class MeshBuilder
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Color> Colors = new List<Color>();
            public readonly List<Vector2> Uvs = new List<Vector2>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<int> Triangles = new List<int>();
            public Matrix4x4 Matrix = Matrix4x4.identity;

            public int VertexCount => Vertices.Count;

            /// <summary>a→b→c→d の順の四角（UV は a=(0,0), b=(0,1), c=(1,1), d=(1,0)）。裏表どちらからも見えるシェーダーで描く。</summary>
            public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
            {
                color = ToVertexColor(color);
                int i = Vertices.Count;
                var wa = Matrix.MultiplyPoint3x4(a);
                var wb = Matrix.MultiplyPoint3x4(b);
                var wc = Matrix.MultiplyPoint3x4(c);
                Vertices.Add(wa); Vertices.Add(wb);
                Vertices.Add(wc); Vertices.Add(Matrix.MultiplyPoint3x4(d));
                // 面の向き（背景用のポイントライトの当たり方に使う。シェーダーは裏表どちらも照らす）。
                var normal = Vector3.Cross(wb - wa, wc - wa).normalized;
                for (int k = 0; k < 4; k++)
                {
                    Normals.Add(normal);
                }
                Uvs.Add(new Vector2(0, 0)); Uvs.Add(new Vector2(0, 1)); Uvs.Add(new Vector2(1, 1)); Uvs.Add(new Vector2(1, 0));
                for (int k = 0; k < 4; k++)
                {
                    Colors.Add(color);
                }
                Triangles.Add(i); Triangles.Add(i + 1); Triangles.Add(i + 2);
                Triangles.Add(i); Triangles.Add(i + 2); Triangles.Add(i + 3);
            }

            /// <summary>水平な四角（中心と大きさ X, Z）。光だまり・影・カードに使う。</summary>
            public void AddFlatQuad(Vector3 center, Vector2 size, Color color)
            {
                float hx = size.x / 2f;
                float hz = size.y / 2f;
                AddQuad(center + new Vector3(-hx, 0f, -hz), center + new Vector3(-hx, 0f, hz),
                    center + new Vector3(hx, 0f, hz), center + new Vector3(hx, 0f, -hz), color);
            }

            /// <summary>
            /// 箱（正面＝+Z側・左右・上面だけ。底と背面はカメラから見えないので作らない）。
            /// backBottom は背面の下の中心、frontTop は正面の上の中心。
            /// </summary>
            public void AddBox(Vector3 backBottom, Vector3 frontTop, float width, Color front, Color side, Color top)
            {
                float x0 = backBottom.x - width / 2f;
                float x1 = backBottom.x + width / 2f;
                float y0 = backBottom.y;
                float y1 = frontTop.y;
                float z0 = backBottom.z;
                float z1 = frontTop.z;
                AddQuad(new Vector3(x0, y0, z1), new Vector3(x0, y1, z1), new Vector3(x1, y1, z1), new Vector3(x1, y0, z1), front);
                AddQuad(new Vector3(x0, y0, z0), new Vector3(x0, y1, z0), new Vector3(x0, y1, z1), new Vector3(x0, y0, z1), side);
                AddQuad(new Vector3(x1, y0, z1), new Vector3(x1, y1, z1), new Vector3(x1, y1, z0), new Vector3(x1, y0, z0), side);
                AddQuad(new Vector3(x0, y1, z1), new Vector3(x0, y1, z0), new Vector3(x1, y1, z0), new Vector3(x1, y1, z1), top);
            }

            /// <summary>
            /// 楕円の柱（側面と上面）。bottomCenter は底面の中心。fromAngle〜toAngle の範囲だけ作る
            /// （半円なら 0〜π。2π 未満のときは、まっすぐな辺の側面も作る）。
            /// </summary>
            public void AddEllipsePrism(Vector3 bottomCenter, float rx, float rz, float height, int segments, Color side, Color top,
                float fromAngle, float toAngle)
            {
                for (int s = 0; s < segments; s++)
                {
                    float a0 = Mathf.Lerp(fromAngle, toAngle, s / (float)segments);
                    float a1 = Mathf.Lerp(fromAngle, toAngle, (s + 1) / (float)segments);
                    var p0 = bottomCenter + new Vector3(Mathf.Cos(a0) * rx, 0f, Mathf.Sin(a0) * rz);
                    var p1 = bottomCenter + new Vector3(Mathf.Cos(a1) * rx, 0f, Mathf.Sin(a1) * rz);
                    // 正面（+Z）ほど少し明るくして丸みを出す。
                    float facing = 0.75f + 0.25f * Mathf.Sin((a0 + a1) / 2f);
                    AddQuad(p0, p0 + Vector3.up * height, p1 + Vector3.up * height, p1, side * facing);
                }
                if (toAngle - fromAngle < Mathf.PI * 2f - 0.001f)
                {
                    var e0 = bottomCenter + new Vector3(Mathf.Cos(fromAngle) * rx, 0f, Mathf.Sin(fromAngle) * rz);
                    var e1 = bottomCenter + new Vector3(Mathf.Cos(toAngle) * rx, 0f, Mathf.Sin(toAngle) * rz);
                    AddQuad(e1, e1 + Vector3.up * height, e0 + Vector3.up * height, e0, side * 0.7f);
                }
                AddEllipseDisc(bottomCenter + Vector3.up * height, rx, rz, segments, top, fromAngle, toAngle);
            }

            /// <summary>水平な楕円の板（fromAngle〜toAngle の扇形）。</summary>
            public void AddEllipseDisc(Vector3 center, float rx, float rz, int segments, Color color, float fromAngle, float toAngle)
            {
                color = ToVertexColor(color);
                int c = Vertices.Count;
                var up = Matrix.MultiplyVector(Vector3.up).normalized;
                Vertices.Add(Matrix.MultiplyPoint3x4(center));
                Uvs.Add(new Vector2(0.5f, 0.5f));
                Colors.Add(color);
                Normals.Add(up);
                for (int s = 0; s <= segments; s++)
                {
                    float a = Mathf.Lerp(fromAngle, toAngle, s / (float)segments);
                    Vertices.Add(Matrix.MultiplyPoint3x4(center + new Vector3(Mathf.Cos(a) * rx, 0f, Mathf.Sin(a) * rz)));
                    Uvs.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f));
                    Colors.Add(color);
                    Normals.Add(up);
                }
                for (int s = 0; s < segments; s++)
                {
                    Triangles.Add(c); Triangles.Add(c + 1 + s); Triangles.Add(c + 2 + s);
                }
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                if (Vertices.Count > 65000)
                {
                    mesh.indexFormat = IndexFormat.UInt32;
                }
                mesh.SetVertices(Vertices);
                mesh.SetColors(Colors);
                mesh.SetUVs(0, Uvs);
                mesh.SetNormals(Normals);
                mesh.SetTriangles(Triangles, 0);
                mesh.RecalculateBounds();
                mesh.MarkDynamic();
                return mesh;
            }
        }
    }
}
