using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LuckyTrash.UI
{
    /// <summary>
    /// STARTボタンで開く、モード選択のカルーセルポップアップ。
    /// 「全国対戦」「友達と対戦」「CPU対戦」の3枚のカードを左右送りで切り替えて表示する。
    /// 非活性モード（全国対戦・友達と対戦）表示中はSTARTボタンも非活性にし、
    /// CPU対戦表示中のみSTARTを押すと <see cref="PlayerCountPopupView"/> を開く。
    /// </summary>
    public class ModeSelectPopupView : MonoBehaviour
    {
        private readonly struct ModeInfo
        {
            public readonly string Title;
            public readonly string Description;
            public readonly bool IsLocked;

            public ModeInfo(string title, string description, bool isLocked)
            {
                Title = title;
                Description = description;
                IsLocked = isLocked;
            }
        }

        // 基本設計書の要約に基づく3モード。CPU対戦のみ今回のスコープで有効。
        private static readonly ModeInfo[] Modes =
        {
            new ModeInfo("全国対戦", "全国のプレイヤーと対戦します。", true),
            new ModeInfo("友達と対戦", "友達と対戦します。", true),
            new ModeInfo("CPU対戦", "人数を決めてローカルで対戦します。", false),
        };

        private const int CpuModeIndex = 2;

        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text _modeTitleText;
        [SerializeField] private TMP_Text _modeDescriptionText;
        [SerializeField] private GameObject _comingSoonBadge;
        [SerializeField] private Button _prevButton;
        [SerializeField] private Button _nextButton;
        [SerializeField] private Image[] _indicatorDots;
        [SerializeField] private Button _startButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private PlayerCountPopupView _playerCountPopupView;

        private readonly Color _indicatorActiveColor = Color.white;
        private readonly Color _indicatorInactiveColor = new Color(1f, 1f, 1f, 0.35f);

        private int _currentIndex;

        private void Awake()
        {
            if (_prevButton != null) _prevButton.onClick.AddListener(() => Move(-1));
            if (_nextButton != null) _nextButton.onClick.AddListener(() => Move(1));
            if (_startButton != null) _startButton.onClick.AddListener(OnStartClicked);
            if (_closeButton != null) _closeButton.onClick.AddListener(Close);
        }

        public void Open()
        {
            _currentIndex = 0;
            RefreshCard();

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

        private void Move(int delta)
        {
            int count = Modes.Length;
            _currentIndex = ((_currentIndex + delta) % count + count) % count;
            RefreshCard();
        }

        private void RefreshCard()
        {
            var mode = Modes[_currentIndex];

            if (_modeTitleText != null) _modeTitleText.text = mode.Title;
            if (_modeDescriptionText != null) _modeDescriptionText.text = mode.Description;
            if (_comingSoonBadge != null) _comingSoonBadge.SetActive(mode.IsLocked);
            if (_startButton != null) _startButton.interactable = !mode.IsLocked;

            if (_indicatorDots != null)
            {
                for (int i = 0; i < _indicatorDots.Length; i++)
                {
                    if (_indicatorDots[i] != null)
                    {
                        _indicatorDots[i].color = i == _currentIndex ? _indicatorActiveColor : _indicatorInactiveColor;
                    }
                }
            }
        }

        private void OnStartClicked()
        {
            if (Modes[_currentIndex].IsLocked)
            {
                return;
            }

            if (_currentIndex == CpuModeIndex)
            {
                Close();
                if (_playerCountPopupView != null)
                {
                    _playerCountPopupView.Open();
                }
            }
        }
    }
}
