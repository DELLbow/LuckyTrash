using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LuckyTrash.UI
{
    /// <summary>
    /// プロフィールパネルのNAMEタップで開く、名前入力ポップアップ。
    /// 入力した名前を PlayerPrefs に保存し、次回起動時にも表示に使えるようにする。
    /// 未設定時のデフォルト名は「Player」。
    /// </summary>
    public class NameInputPopupView : MonoBehaviour
    {
        private const string PlayerNamePrefKey = "LuckyTrash.PlayerName";
        private const string DefaultPlayerName = "Player";

        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_InputField _nameInputField;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private Button _closeButton;

        /// <summary>
        /// 名前が確定（保存）されたときに通知する。引数は確定後の新しい名前。
        /// </summary>
        public event Action<string> NameConfirmed;

        /// <summary>
        /// PlayerPrefs に保存済みの名前を取得する。未設定なら「Player」を返す。
        /// </summary>
        public static string LoadPlayerName()
        {
            return PlayerPrefs.GetString(PlayerNamePrefKey, DefaultPlayerName);
        }

        private void Awake()
        {
            if (_confirmButton != null)
            {
                _confirmButton.onClick.AddListener(OnConfirmClicked);
            }

            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(Close);
            }
        }

        public void Open(string currentName)
        {
            if (_nameInputField != null)
            {
                _nameInputField.text = currentName;
            }

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

        private void OnConfirmClicked()
        {
            string newName = _nameInputField != null ? _nameInputField.text.Trim() : string.Empty;
            if (string.IsNullOrEmpty(newName))
            {
                newName = DefaultPlayerName;
            }

            PlayerPrefs.SetString(PlayerNamePrefKey, newName);
            PlayerPrefs.Save();

            NameConfirmed?.Invoke(newName);
            Close();
        }
    }
}
