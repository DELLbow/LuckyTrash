using System.Linq;
using LuckyTrash.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LuckyTrash.Controllers
{
    /// <summary>
    /// TitleScene に配置し、画面全体を統括するコントローラー。
    /// プロフィールパネル（NAME/LIFE表示）の初期化・更新と、STARTボタン・NAMEタップ・
    /// 下部メニュー（ルール確認・設定）からの各ポップアップ起動を仲介する。
    /// モード選択・人数選択・名前入力・ルール確認・設定の各ポップアップ自体の表示ロジックは
    /// それぞれの View コンポーネント（<see cref="LuckyTrash.UI"/> 名前空間）に委譲するが、
    /// 人数選択ポップアップで選ばれた人数を <see cref="GameManager"/> に予約して GameScene へ
    /// 遷移する処理（UI層を持たないロジック）はここで行う。
    /// </summary>
    public class TitleSceneController : MonoBehaviour
    {
        private const string GameSceneName = "GameScene";
        private const int LifeMax = 5;
        // カード表示（CardView）と同じ ♥ (U+2665) を使う。MSGothic SDF フォントで表示確認済みの文字。
        private const string FullHeart = "♥";

        [Header("Profile")]
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _lifeText;
        [SerializeField] private Button _nameTapButton;

        [Header("Start")]
        [SerializeField] private Button _startButton;
        [SerializeField] private ModeSelectPopupView _modeSelectPopup;

        [Header("Name Input")]
        [SerializeField] private NameInputPopupView _nameInputPopup;

        [Header("Player Count")]
        [SerializeField] private PlayerCountPopupView _playerCountPopup;

        [Header("Bottom Bar")]
        [SerializeField] private Button _rulesButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private SimplePopupView _rulesPopup;
        [SerializeField] private SimplePopupView _settingsPopup;

        private void Awake()
        {
            RefreshPlayerName();
            RefreshLife();

            if (_startButton != null) _startButton.onClick.AddListener(() => _modeSelectPopup?.Open());
            if (_nameTapButton != null) _nameTapButton.onClick.AddListener(OnNameTapped);
            if (_rulesButton != null) _rulesButton.onClick.AddListener(() => _rulesPopup?.Open());
            if (_settingsButton != null) _settingsButton.onClick.AddListener(() => _settingsPopup?.Open());

            if (_nameInputPopup != null)
            {
                _nameInputPopup.NameConfirmed += OnNameConfirmed;
            }

            if (_playerCountPopup != null)
            {
                _playerCountPopup.PlayerCountSelected += OnPlayerCountSelected;
            }
        }

        private void RefreshPlayerName()
        {
            if (_nameText != null)
            {
                _nameText.text = NameInputPopupView.LoadPlayerName();
            }
        }

        private void RefreshLife()
        {
            if (_lifeText != null)
            {
                _lifeText.text = string.Concat(Enumerable.Repeat(FullHeart, LifeMax));
            }
        }

        private void OnNameTapped()
        {
            _nameInputPopup?.Open(NameInputPopupView.LoadPlayerName());
        }

        private void OnNameConfirmed(string newName)
        {
            if (_nameText != null)
            {
                _nameText.text = newName;
            }
        }

        /// <summary>
        /// 人数選択ポップアップで人数が選ばれたとき: 選択人数を GameManager に予約したうえで
        /// GameScene をロードする（人数選択パネルをスキップしてそのままゲームが始まる）。
        /// </summary>
        private void OnPlayerCountSelected(int playerCount)
        {
            GameManager.Instance.RequestPlayerCount(playerCount);
            SceneManager.LoadScene(GameSceneName);
        }
    }
}
