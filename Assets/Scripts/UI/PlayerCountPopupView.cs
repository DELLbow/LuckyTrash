using System;
using UnityEngine;
using UnityEngine.UI;

namespace LuckyTrash.UI
{
    /// <summary>
    /// モード選択ポップアップで「CPU対戦」を選んだ後に開く、新デザインの人数選択ポップアップ。
    /// このコンポーネント自体はシーン遷移や GameManager 呼び出しを行わない（UI層はロジックを
    /// 持たない既存の流儀に合わせる）。2人/3人/4人のいずれかが選ばれたことを
    /// <see cref="PlayerCountSelected"/> で通知するだけで、実際の予約・シーン遷移は
    /// TitleSceneController（Controllers層）が担う。
    /// </summary>
    public class PlayerCountPopupView : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Button _twoPlayerButton;
        [SerializeField] private Button _threePlayerButton;
        [SerializeField] private Button _fourPlayerButton;

        /// <summary>2/3/4人のいずれかのボタンが押されたときに、選ばれた人数を通知する。</summary>
        public event Action<int> PlayerCountSelected;

        private void Awake()
        {
            if (_twoPlayerButton != null) _twoPlayerButton.onClick.AddListener(() => Select(2));
            if (_threePlayerButton != null) _threePlayerButton.onClick.AddListener(() => Select(3));
            if (_fourPlayerButton != null) _fourPlayerButton.onClick.AddListener(() => Select(4));
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

        private void Select(int playerCount)
        {
            PlayerCountSelected?.Invoke(playerCount);
        }
    }
}
