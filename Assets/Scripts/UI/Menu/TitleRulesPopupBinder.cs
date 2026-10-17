using LuckyTrash.UI.Rules;
using TMPro;
using UnityEngine;

namespace LuckyTrash.UI.Menu
{
    /// <summary>
    /// タイトル画面の「ルール確認」のポップアップ（SimplePopupView）の本文を、共通のルール説明（RulesText.asset）に差し替える。
    /// 元の固定の本文（BodyText）は隠し、同じ場所にスクロールできるルール説明を作る。ポップアップの開閉はこれまでどおり。
    /// </summary>
    public class TitleRulesPopupBinder : MonoBehaviour
    {
        [Tooltip("ルール説明の文章（Assets/Settings/RulesText.asset。試合中のメニューと同じもの）。")]
        [SerializeField] private RulesContent _rules;
        [Tooltip("元の固定の本文（隠して、この位置にルール説明を出す）。")]
        [SerializeField] private RectTransform _bodyText;
        [SerializeField] private TMP_FontAsset _font;
        [SerializeField] private float _fontSize = 28f;
        [Tooltip("文字色（タイトル画面のポップアップは紺地なので明るい色）。")]
        [SerializeField] private Color _textColor = new Color(0.93f, 0.93f, 0.95f);
        [Tooltip("ルール説明の表示範囲の、元の本文に対する上下の伸ばし幅。")]
        [SerializeField] private float _extraHeight = 20f;
        [Tooltip("表示範囲の下端がこのボタン（CLOSE）にかからないようにする。")]
        [SerializeField] private RectTransform _bottomLimit;
        [SerializeField] private float _bottomGap = 24f;

        private bool _built;

        private void Awake()
        {
            Build();
        }

        private void Build()
        {
            if (_built || _bodyText == null)
            {
                return;
            }
            _built = true;

            var area = MenuUiKit.CreateRect("RulesArea", _bodyText.parent, _bodyText.sizeDelta + new Vector2(0f, _extraHeight));
            area.anchorMin = _bodyText.anchorMin;
            area.anchorMax = _bodyText.anchorMax;
            area.pivot = _bodyText.pivot;
            area.anchoredPosition = _bodyText.anchoredPosition;
            area.SetSiblingIndex(_bodyText.GetSiblingIndex());
            KeepAbove(area);
            _bodyText.gameObject.SetActive(false);

            var font = _font;
            if (font == null)
            {
                var original = _bodyText.GetComponent<TMP_Text>();
                font = original != null ? original.font : null;
            }
            RulesScrollView.Build(area, _rules, font, _fontSize, _textColor);
        }

        /// <summary>表示範囲の下端を、CLOSE ボタンの上端より _bottomGap だけ上に収める。</summary>
        private void KeepAbove(RectTransform area)
        {
            if (_bottomLimit == null)
            {
                return;
            }
            var corners = new Vector3[4];
            _bottomLimit.GetWorldCorners(corners);
            float limitTop = area.parent.InverseTransformPoint(corners[1]).y + _bottomGap;
            area.GetWorldCorners(corners);
            float areaBottom = area.parent.InverseTransformPoint(corners[0]).y;
            if (areaBottom < limitTop)
            {
                area.offsetMin += new Vector2(0f, limitTop - areaBottom);
            }
        }
    }
}
