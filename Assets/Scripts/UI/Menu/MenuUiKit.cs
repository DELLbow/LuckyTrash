using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LuckyTrash.UI.Menu
{
    /// <summary>メニュー・確認・ルール説明のパネルで共通に使う、紙の板・ボタン・文字の組み立て（新しい画像素材は使わない）。</summary>
    public static class MenuUiKit
    {
        public static readonly Color PaperColor = new Color(1f, 0.98f, 0.92f);
        public static readonly Color PaperEdgeColor = new Color(0.55f, 0.52f, 0.46f, 0.9f);
        public static readonly Color SlipInkColor = new Color(0.30f, 0.32f, 0.36f);
        public static readonly Color NumberColor = new Color(0.12f, 0.12f, 0.16f);
        public static readonly Color Gold = new Color(0.83f, 0.66f, 0.18f);

        public static RectTransform CreateRect(string name, Transform parent, Vector2 size)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            return rect;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            if (sprite != null)
            {
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 3f;
            }
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static TextMeshProUGUI CreateText(string name, Transform parent, TMP_FontAsset font, string text, float size,
            Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<TextMeshProUGUI>();
            if (font != null)
            {
                label.font = font;
            }
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = align;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            return label;
        }

        /// <summary>
        /// 画面全体を覆う半透明の暗い幕（後ろの盤面やボタンを押せないようにタップを受け止める）。
        /// alpha は「見た目で何割暗くするか」。リニア色空間では半透明の黒が薄く見える（0.55 で約3割しか暗くならない）ので、見た目どおりになるよう換算する。
        /// </summary>
        public static Image CreateBackdrop(Transform parent, float alpha)
        {
            if (QualitySettings.activeColorSpace == ColorSpace.Linear)
            {
                alpha = 1f - Mathf.Pow(1f - Mathf.Clamp01(alpha), 2.2f);
            }
            var backdrop = CreateImage("Backdrop", parent, null, new Color(0f, 0f, 0f, alpha));
            Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = true;
            return backdrop;
        }

        /// <summary>伝票・明細書と同じアイボリーの紙（影・縁・上部の「LUCKY TRASH BANK」・罫線）。</summary>
        public static RectTransform CreatePaper(Transform parent, Vector2 size, Sprite roundedSprite, TMP_FontAsset symbolFont,
            bool withBankHeader = true)
        {
            var paper = CreateRect("Paper", parent, size);
            var shadow = CreateImage("Shadow", paper, roundedSprite, new Color(0f, 0f, 0f, 0.35f));
            shadow.rectTransform.sizeDelta = size;
            shadow.rectTransform.anchoredPosition = new Vector2(8f, -10f);
            var sheet = CreateImage("Sheet", paper, roundedSprite, PaperColor);
            sheet.rectTransform.sizeDelta = size;
            sheet.raycastTarget = true;
            var edge = sheet.gameObject.AddComponent<Outline>();
            edge.effectColor = PaperEdgeColor;
            edge.effectDistance = new Vector2(1.5f, -1.5f);

            if (withBankHeader)
            {
                var bank = CreateText("BankName", paper, symbolFont, "LUCKY TRASH BANK", 18f, SlipInkColor, TextAlignmentOptions.Center);
                bank.fontStyle = FontStyles.Bold;
                bank.characterSpacing = 14f;
                bank.rectTransform.sizeDelta = new Vector2(size.x - 60f, 28f);
                bank.rectTransform.anchoredPosition = new Vector2(0f, size.y / 2f - 28f);
                var rule = CreateImage("Rule", paper, null, new Color(SlipInkColor.r, SlipInkColor.g, SlipInkColor.b, 0.45f));
                rule.rectTransform.sizeDelta = new Vector2(size.x - 60f, 2f);
                rule.rectTransform.anchoredPosition = new Vector2(0f, size.y / 2f - 48f);
            }
            return paper;
        }

        /// <summary>
        /// 紙の上の、インクの枠付きのボタン（明細書の雰囲気に合わせた長方形）。filled なら塗りつぶし（主な操作）。
        /// 押したときの沈み込みは一時停止中でも動く（unscaled）。
        /// </summary>
        public static Button CreateInkButton(Transform parent, string label, Vector2 size, Vector2 position, TMP_FontAsset font,
            Sprite roundedSprite, Color ink, bool filled, Action onClick)
        {
            var root = new GameObject("Button_" + label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            var rect = (RectTransform)root.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            var body = root.GetComponent<Image>();
            body.sprite = roundedSprite;
            body.type = Image.Type.Sliced;
            body.pixelsPerUnitMultiplier = 3f;
            body.color = filled ? ink : PaperColor;
            var frame = root.AddComponent<Outline>();
            frame.effectColor = ink;
            frame.effectDistance = new Vector2(2f, -2f);

            var text = CreateText("Label", rect, font, label, size.y * 0.42f, filled ? PaperColor : ink, TextAlignmentOptions.Center);
            text.fontStyle = FontStyles.Bold;
            text.rectTransform.sizeDelta = size;

            var button = root.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.95f, 0.95f, 0.95f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f);
            colors.fadeDuration = 0.05f;
            button.colors = colors;
            button.onClick.AddListener(() => onClick?.Invoke());
            return button;
        }
    }
}
