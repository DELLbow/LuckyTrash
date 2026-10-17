using LuckyTrash.UI.Menu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LuckyTrash.UI.Rules
{
    /// <summary>
    /// ルール説明の文章を、縦にスクロールできる枠に表示する部品（タイトル画面と試合中のメニューで共用）。
    /// 文章は <see cref="RulesContent"/> から読み込む。マウスのホイール・ドラッグ・スクロールバーで動かせる。
    /// </summary>
    public static class RulesScrollView
    {
        /// <summary>area の中いっぱいに、スクロールできるルール説明を作る。</summary>
        public static ScrollRect Build(RectTransform area, RulesContent content, TMP_FontAsset font, float fontSize, Color textColor)
        {
            var viewport = MenuUiKit.CreateRect("RulesViewport", area, Vector2.zero);
            MenuUiKit.Stretch(viewport);
            viewport.offsetMax = new Vector2(-22f, 0f); // 右端はスクロールバーの分だけ空ける
            viewport.gameObject.AddComponent<RectMask2D>();
            // ドラッグ・ホイールを受け取るための透明な板。
            var hit = viewport.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);

            var text = MenuUiKit.CreateText("RulesText", viewport, font,
                content != null ? content.BuildRichText() : RulesTextFormatter.Format(RulesContent.DefaultSource, null),
                fontSize, textColor, TextAlignmentOptions.TopLeft);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.richText = true;
            text.lineSpacing = 12f;
            text.paragraphSpacing = 8f;
            var textRect = text.rectTransform;
            textRect.anchorMin = new Vector2(0f, 1f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.pivot = new Vector2(0.5f, 1f);
            textRect.offsetMin = new Vector2(8f, 0f);
            textRect.offsetMax = new Vector2(-8f, 0f);
            var fitter = text.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 細いスクロールバー（紙の上で目立ちすぎない墨色）。
            var barRoot = new GameObject("Scrollbar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Scrollbar));
            var barRect = (RectTransform)barRoot.transform;
            barRect.SetParent(area, false);
            barRect.anchorMin = new Vector2(1f, 0f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(1f, 0.5f);
            barRect.sizeDelta = new Vector2(10f, 0f);
            barRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.08f);
            var handleArea = MenuUiKit.CreateRect("SlidingArea", barRect, Vector2.zero);
            MenuUiKit.Stretch(handleArea);
            var handle = MenuUiKit.CreateImage("Handle", handleArea, null, new Color(MenuUiKit.SlipInkColor.r, MenuUiKit.SlipInkColor.g, MenuUiKit.SlipInkColor.b, 0.6f));
            MenuUiKit.Stretch(handle.rectTransform);
            handle.raycastTarget = true;
            var scrollbar = barRoot.GetComponent<Scrollbar>();
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.targetGraphic = handle;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;

            var scroll = area.gameObject.GetComponent<ScrollRect>();
            if (scroll == null)
            {
                scroll = area.gameObject.AddComponent<ScrollRect>();
            }
            scroll.viewport = viewport;
            scroll.content = textRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 1f;
            return scroll;
        }
    }
}
