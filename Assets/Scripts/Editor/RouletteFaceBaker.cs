using System.IO;
using UnityEditor;
using UnityEngine;

namespace LuckyTrash.UI.Editor
{
    /// <summary>
    /// <see cref="RouletteWheelDecorator"/>が管理する円盤（扇形メッシュ・3Dラベル・仕切り線・中心キャップ）を
    /// 真上から正投影カメラで撮影し、1枚のテクスチャに焼き込んで保存するEditor専用ツール。
    /// 撮影時のカメラの向き（up=円盤ローカル+Z, right=円盤ローカル+X）は、
    /// RouletteWheelDecoratorが生成する円形メッシュのUV計算式
    /// "u=(x/DiscRadius)*0.5+0.5, v=(z/DiscRadius)*0.5+0.5" と完全に対応させてあるため、
    /// 焼き込み後も扇形の位置・角度はRouletteAngleMathの計算とズレない。
    /// </summary>
    public static class RouletteFaceBaker
    {
        // シーン上の未使用レイヤー。撮影したいオブジェクトだけを一時的にこのレイヤーへ移し、
        // カメラのcullingMaskをこのレイヤーだけに絞ることで、他のオブジェクト（リム・台座・テーブル等）を
        // 一切映り込ませずに円盤の中身だけを撮影する。
        private const int BakeLayer = 10;
        private const float CaptureHeight = 3f;

        [MenuItem("Tools/LuckyTrash/ルーレット：マス面テクスチャを焼き込む")]
        public static void BakeFromMenu()
        {
            var decorator = Object.FindFirstObjectByType<RouletteWheelDecorator>();
            if (decorator == null)
            {
                Debug.LogError("[RouletteFaceBaker] シーン内にRouletteWheelDecoratorが見つかりません。");
                return;
            }

            Bake(decorator);
        }

        public static void Bake(RouletteWheelDecorator decorator)
        {
            var so = new SerializedObject(decorator);
            var discProp = so.FindProperty("_disc");
            var disc = discProp.objectReferenceValue as Transform;
            if (disc == null)
            {
                Debug.LogError("[RouletteFaceBaker] _disc が未設定です。");
                return;
            }

            int resolution = Mathf.Max(64, so.FindProperty("_bakeResolution").intValue);
            string assetPath = so.FindProperty("_bakeOutputPath").stringValue;
            bool originalHideFlag = so.FindProperty("_hideOriginalFaceContent").boolValue;

            // 撮影対象（元の扇形・ラベル・仕切り線・キャップ）を必ず表示状態にしてから撮る。
            so.FindProperty("_hideOriginalFaceContent").boolValue = false;
            so.ApplyModifiedProperties();
            decorator.Rebuild();

            Quaternion originalDiscRotation = disc.localRotation;
            disc.localRotation = Quaternion.identity;

            var movedObjects = new System.Collections.Generic.List<(GameObject go, int originalLayer)>();
            CollectBakeTargets(disc, movedObjects);
            foreach (var entry in movedObjects)
            {
                SetLayerRecursively(entry.go, BakeLayer);
            }

            Texture2D baked = null;
            try
            {
                baked = CaptureTopDown(disc.position, RouletteWheelDecorator.DiscRadius, resolution, BakeLayer);
            }
            finally
            {
                foreach (var entry in movedObjects)
                {
                    SetLayerRecursively(entry.go, entry.originalLayer);
                }
                disc.localRotation = originalDiscRotation;
            }

            if (baked == null)
            {
                Debug.LogError("[RouletteFaceBaker] 撮影に失敗しました。");
                so.FindProperty("_hideOriginalFaceContent").boolValue = originalHideFlag;
                so.ApplyModifiedProperties();
                decorator.Rebuild();
                return;
            }

            float edgeRingWidth = so.FindProperty("_edgeRingWidth").floatValue;
            float edgeRingDarkness = so.FindProperty("_edgeRingDarkness").floatValue;
            if (edgeRingWidth > 0f)
            {
                ApplyEdgeDarkening(baked, RouletteWheelDecorator.DiscRadius, edgeRingWidth, edgeRingDarkness);
            }

            string savedPath = SaveTextureAsPng(baked, assetPath, resolution);
            Object.DestroyImmediate(baked);

            AssetDatabase.Refresh();
            var texAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(savedPath);

            so.FindProperty("_bakedFaceTexture").objectReferenceValue = texAsset;
            so.FindProperty("_hideOriginalFaceContent").boolValue = originalHideFlag;
            so.ApplyModifiedProperties();

            decorator.Rebuild();

            Debug.Log("[RouletteFaceBaker] 焼き込み完了: " + savedPath);
        }

        /// <summary>
        /// Disc直下の6枚の扇形(Slice_0..5)・6枚のラベル(_sliceLabels)・
        /// GeneratedDecorations内のDivider_0..5・CenterCapを撮影対象として収集する。
        /// 既存の焼き込み結果(BakedFace)やハイライトオーバーレイ(HighlightOverlay)は対象に含めない。
        /// </summary>
        private static void CollectBakeTargets(Transform disc, System.Collections.Generic.List<(GameObject, int)> result)
        {
            for (int i = 0; i < 6; i++)
            {
                var slice = disc.Find("Slice_" + i);
                if (slice != null)
                {
                    result.Add((slice.gameObject, slice.gameObject.layer));
                }

                var label = disc.Find("Label_" + i);
                if (label != null)
                {
                    result.Add((label.gameObject, label.gameObject.layer));
                }
            }

            var genRoot = disc.Find("GeneratedDecorations");
            if (genRoot != null)
            {
                for (int i = 0; i < 6; i++)
                {
                    var divider = genRoot.Find("Divider_" + i);
                    if (divider != null)
                    {
                        result.Add((divider.gameObject, divider.gameObject.layer));
                    }
                }

                var cap = genRoot.Find("CenterCap");
                if (cap != null)
                {
                    result.Add((cap.gameObject, cap.gameObject.layer));
                }
            }
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        /// <summary>
        /// centerの真上から、半径radiusの正方形範囲を正投影カメラで撮影しTexture2Dを返す。
        /// カメラの向きは up=ワールド+Z, right=ワールド+X に固定しているため、
        /// 戻り値のTexture2DのUV(u,v)は、撮影時点のワールド(x,z) = center + ((u-0.5)*2*radius, (v-0.5)*2*radius)
        /// に対応する。disc(親チェーンが単位回転・スケール1であること)のローカル(x,z)をそのままworldとして扱える前提。
        /// </summary>
        private static Texture2D CaptureTopDown(Vector3 center, float radius, int resolution, int cullingMask)
        {
            var camGo = new GameObject("RouletteFaceBakeCamera") { hideFlags = HideFlags.HideAndDontSave };
            var cam = camGo.AddComponent<Camera>();
            camGo.transform.position = center + Vector3.up * CaptureHeight;
            camGo.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);

            cam.orthographic = true;
            cam.orthographicSize = radius;
            cam.aspect = 1f;
            cam.cullingMask = 1 << cullingMask;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = CaptureHeight + 1f;
            cam.allowHDR = false;
            cam.allowMSAA = false;

            var rt = new RenderTexture(resolution, resolution, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 1
            };
            cam.targetTexture = rt;

            Texture2D tex = null;
            try
            {
                cam.Render();

                var prevActive = RenderTexture.active;
                RenderTexture.active = rt;
                tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false, false);
                tex.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
                tex.Apply();
                RenderTexture.active = prevActive;
            }
            finally
            {
                cam.targetTexture = null;
                rt.Release();
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(camGo);
            }

            return tex;
        }

        /// <summary>
        /// 円盤外周に暗いリングを焼き込む（別メッシュは追加せず、テクスチャのピクセルを直接暗くする）。
        /// 円の中心からの距離が (DiscRadius-ringWidth)〜DiscRadius の範囲を、外周に向かって
        /// darkness倍まで徐々に暗くする。円の外側（テクスチャ四隅）はメッシュから一切サンプルされないため触らない。
        /// </summary>
        private static void ApplyEdgeDarkening(Texture2D tex, float discRadius, float ringWidthWorld, float darkness)
        {
            int w = tex.width;
            int h = tex.height;
            float ringWidthFraction = Mathf.Clamp01(ringWidthWorld / discRadius);
            float innerR = 1f - ringWidthFraction;
            var pixels = tex.GetPixels();

            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;
                float dy = (v - 0.5f) * 2f;
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w;
                    float dx = (u - 0.5f) * 2f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    if (r <= innerR || r > 1f)
                    {
                        continue;
                    }

                    float t = Mathf.InverseLerp(innerR, 1f, r);
                    float mult = Mathf.Lerp(1f, darkness, t);
                    int idx = y * w + x;
                    Color c = pixels[idx];
                    pixels[idx] = new Color(c.r * mult, c.g * mult, c.b * mult, c.a);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
        }

        private static string SaveTextureAsPng(Texture2D tex, string assetPath, int resolution)
        {
            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllBytes(fullPath, tex.EncodeToPNG());

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.alphaIsTransparency = false;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = Mathf.NextPowerOfTwo(resolution);
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            return assetPath;
        }
    }
}
