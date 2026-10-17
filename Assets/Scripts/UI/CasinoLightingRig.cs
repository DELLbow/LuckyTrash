using System;
using UnityEngine;

namespace LuckyTrash.UI
{
    /// <summary>
    /// テーブルの照明を部屋（暖かい光のカジノのフロア）になじませるための照明の設定。
    /// Directional Light の色・明るさ、テーブルの真上の暖かいスポットライト（影なし）、テーブル周りの床の光だまりの色を、
    /// 「変更前」「案1（控えめ）」「案2（しっかり）」の3つの値の組から選んで当てる（Inspector の Preset で切り替えて比べられる）。
    /// 背景の物だけを照らすポイントライト（影なし）の色・明るさ・届く距離もここで決める。
    /// ライトの向き・影・Rendering Layers（テーブル用と背景用の分け方）はシーン側の設定を使い、ここでは触らない。
    /// </summary>
    [ExecuteAlways]
    public class CasinoLightingRig : MonoBehaviour
    {
        public enum Preset
        {
            [Tooltip("今回の調整前（白い光で均一に照らす）。")] Original,
            [Tooltip("案1：Directional Light を少し暗く・わずかに暖かく。スポットライトは弱め。")] Subtle,
            [Tooltip("案2：Directional Light をさらに暗く・暖かく。スポットライトで中央を強く照らし、縁が少し沈む。")] Strong,
        }

        /// <summary>テーブルの照明の値の組。</summary>
        [Serializable]
        public class TableLighting
        {
            [Tooltip("Directional Light の明るさ。")] public float DirectionalIntensity = 1f;
            [Tooltip("Directional Light の色。")] public Color DirectionalColor = Color.white;
            [Tooltip("テーブルの真上のスポットライトの明るさ（0で消す）。")] public float SpotIntensity;
            [Tooltip("スポットライトの色。")] public Color SpotColor = Color.white;
            [Tooltip("スポットライトの、明るさが落ち始める角度（度）。")] public float SpotInnerAngle = 30f;
            [Tooltip("スポットライトの、光が届く外側の角度（度）。")] public float SpotOuterAngle = 80f;
            [Tooltip("テーブル周りの床の光だまりの色と濃さ。")] public Color FloorPoolColor = Color.white;
        }

        [Header("比べる案")]
        [Tooltip("どの値の組を当てるか。")]
        [SerializeField] private Preset _preset = Preset.Subtle;
        [SerializeField] private TableLighting _original = new TableLighting
        {
            DirectionalIntensity = 1f,
            DirectionalColor = new Color(1f, 0.957f, 0.839f),
            SpotIntensity = 0f,
            SpotColor = new Color(1f, 0.82f, 0.6f),
            SpotInnerAngle = 30f,
            SpotOuterAngle = 80f,
            FloorPoolColor = new Color(1f, 0.55f, 0.40f, 0.34f)
        };
        [SerializeField] private TableLighting _subtle = new TableLighting
        {
            DirectionalIntensity = 0.82f,
            DirectionalColor = new Color(1f, 0.93f, 0.80f),
            SpotIntensity = 8f,
            SpotColor = new Color(1f, 0.88f, 0.72f),
            SpotInnerAngle = 30f,
            SpotOuterAngle = 110f,
            FloorPoolColor = new Color(1f, 0.58f, 0.38f, 0.38f)
        };
        [SerializeField] private TableLighting _strong = new TableLighting
        {
            DirectionalIntensity = 0.5f,
            DirectionalColor = new Color(1f, 0.86f, 0.68f),
            SpotIntensity = 18f,
            SpotColor = new Color(1f, 0.88f, 0.72f),
            SpotInnerAngle = 15f,
            SpotOuterAngle = 105f,
            FloorPoolColor = new Color(1f, 0.56f, 0.34f, 0.44f)
        };

        [Header("ライト")]
        [Tooltip("テーブルを照らしている Directional Light（向き・影はシーンの設定のまま）。")]
        [SerializeField] private Light _directionalLight;
        [Tooltip("テーブルの真上の暖かいスポットライト（影なし。Rendering Layers でテーブル側だけを照らす）。" +
            "ルーレットの真上だと盤面の青が白っぽく飛ぶので、少し手前（手札側）に寄せて真下に向けている。")]
        [SerializeField] private Light _tableSpot;
        [Tooltip("背景の物だけを照らすポイントライト（影なし。Rendering Layers で背景用のレイヤーだけを照らす）。位置は各ライトの Transform で調整する。")]
        [SerializeField] private Light[] _roomLights;
        [Tooltip("背景用のポイントライトの色（暗くて暖かい色）。")]
        [SerializeField] private Color _roomLightColor = new Color(1f, 0.62f, 0.36f);
        [Tooltip("背景用のポイントライトの明るさ。")]
        [SerializeField] private float _roomLightIntensity = 18f;
        [Tooltip("背景用のポイントライトが届く距離（テーブルまで届かない距離にする）。")]
        [SerializeField] private float _roomLightRange = 19f;

        [Header("背景")]
        [Tooltip("床の光だまりの色を合わせる背景。")]
        [SerializeField] private CasinoBackgroundView _background;

        private bool _dirty;

        /// <summary>いま当てている案の値。</summary>
        public TableLighting Current
        {
            get
            {
                switch (_preset)
                {
                    case Preset.Original: return _original;
                    case Preset.Strong: return _strong;
                    default: return _subtle;
                }
            }
        }

        /// <summary>案を切り替えて、すぐに当てる（撮り比べ用）。</summary>
        public void SetPreset(Preset preset)
        {
            _preset = preset;
            Apply();
        }

        private void OnEnable()
        {
            Apply();
        }

        private void OnValidate()
        {
            _dirty = true;
        }

        private void Update()
        {
            if (_dirty)
            {
                _dirty = false;
                Apply();
            }
        }

        private void Apply()
        {
            var values = Current;
            if (_directionalLight != null)
            {
                _directionalLight.intensity = values.DirectionalIntensity;
                _directionalLight.color = values.DirectionalColor;
            }
            if (_tableSpot != null)
            {
                _tableSpot.enabled = values.SpotIntensity > 0f;
                _tableSpot.intensity = values.SpotIntensity;
                _tableSpot.color = values.SpotColor;
                _tableSpot.innerSpotAngle = Mathf.Min(values.SpotInnerAngle, values.SpotOuterAngle);
                _tableSpot.spotAngle = values.SpotOuterAngle;
                _tableSpot.shadows = LightShadows.None;
            }
            if (_roomLights != null)
            {
                foreach (var light in _roomLights)
                {
                    if (light == null)
                    {
                        continue;
                    }
                    light.color = _roomLightColor;
                    light.intensity = _roomLightIntensity;
                    light.range = _roomLightRange;
                    light.shadows = LightShadows.None;
                }
            }
            if (_background != null)
            {
                _background.SetFloorPoolColor(values.FloorPoolColor);
            }
        }
    }
}
