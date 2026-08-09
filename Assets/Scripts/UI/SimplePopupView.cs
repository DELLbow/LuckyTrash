using UnityEngine;
using UnityEngine.UI;

namespace LuckyTrash.UI
{
    /// <summary>
    /// タイトルバー＋固定テキスト＋CLOSEボタンのみで完結する汎用ポップアップ。
    /// ルール確認ポップアップ、設定ポップアップ（現状は中身が空）で共用する。
    /// </summary>
    public class SimplePopupView : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Button _closeButton;

        private void Awake()
        {
            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(Close);
            }
        }

        public void Open()
        {
            if (_panel != null)
            {
                _panel.SetActive(true);
            }
        }

        public void Close()
        {
            if (_panel != null)
            {
                _panel.SetActive(false);
            }
        }
    }
}
