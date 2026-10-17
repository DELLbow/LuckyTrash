using UnityEngine;
using UnityEngine.UI;

namespace LuckyTrash.UI
{
    /// <summary>
    /// 画面の四隅をほんのり暗くする（ビネット）。ポストプロセスは使わず、Canvas の一番奥（盤面の上、すべての UI の下）に
    /// 中央が透明で四隅に向かって暗くなる半透明の画像を重ねるだけ。画像は起動時にコードで作る（画像素材は使わない）。
    /// UI より奥に置くので、ネームプレート・山札表示・ボタン・紙幣・スタンプなどの UI は暗くならない。
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class VignetteOverlay : MonoBehaviour
    {
        [Tooltip("ビネットを表示するか（入れる・入れないの撮り比べ用）。")]
        [SerializeField] private bool _visible = false;
        [Tooltip("四隅の暗さ（0で無し、1で四隅が真っ暗）。")]
        [Range(0f, 1f)] [SerializeField] private float _strength = 0.45f;
        [Tooltip("暗くなり始める位置（中心からの距離、0〜1。大きいほど四隅だけが暗くなる）。")]
        [Range(0f, 1f)] [SerializeField] private float _innerRadius = 0.55f;
        [Tooltip("暗くする色（ほぼ黒の焦げ茶）。")]
        [SerializeField] private Color _color = new Color(0.03f, 0.02f, 0.015f);

        private RawImage _image;
        private Texture2D _texture;
        private float _builtInnerRadius = -1f;

        private void Awake()
        {
            _image = GetComponent<RawImage>();
            _image.raycastTarget = false;
            Apply();
        }

        private void OnValidate()
        {
            if (Application.isPlaying && _image != null)
            {
                Apply();
            }
        }

        private void OnDestroy()
        {
            if (_texture != null)
            {
                Destroy(_texture);
            }
        }

        /// <summary>表示の ON/OFF を切り替える。</summary>
        public void SetVisible(bool visible)
        {
            _visible = visible;
            Apply();
        }

        private void Apply()
        {
            if (_image == null)
            {
                return;
            }
            if (_texture == null || !Mathf.Approximately(_builtInnerRadius, _innerRadius))
            {
                BuildTexture();
            }
            _image.texture = _texture;
            _image.color = new Color(_color.r, _color.g, _color.b, _strength);
            _image.enabled = _visible && _strength > 0f;
        }

        private void BuildTexture()
        {
            const int size = 128;
            if (_texture == null)
            {
                _texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.DontSave
                };
            }
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 画面の縦横比に関わらず、四隅（対角）で 1 になる距離。
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v) / Mathf.Sqrt(2f);
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(_innerRadius, 1f, r));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            _texture.SetPixels32(pixels);
            _texture.Apply();
            _builtInnerRadius = _innerRadius;
        }
    }
}
