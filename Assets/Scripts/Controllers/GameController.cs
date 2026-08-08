using System;
using System.Linq;
using System.Text;
using LuckyTrash.Cards;
using LuckyTrash.Game;
using LuckyTrash.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LuckyTrash.Controllers
{
    /// <summary>
    /// GameScene に配置し、ゲーム進行全体を統括する開発用コントローラー。
    /// 人数選択パネルで 2〜4人 を選ぶとその人数で GameSetup によりゲームを初期化し、
    /// GameState を介してターンを進行しながら、座席の HandView と CenterArea の
    /// ステータス表示・「次のターン」ボタンを繋ぐ。
    /// ResultScene への遷移や GameManager シングルトン化は次のステップで対応する想定で、
    /// ここでは GameScene 内で完結させている。
    /// </summary>
    public class GameController : MonoBehaviour
    {
        // 4座席分の HandView（基本設計書3.2節: Bottom/Right/Top/Left）。
        [SerializeField] private HandView _bottomHandView;
        [SerializeField] private HandView _rightHandView;
        [SerializeField] private HandView _topHandView;
        [SerializeField] private HandView _leftHandView;

        [SerializeField] private Button _nextTurnButton;
        [SerializeField] private TMP_Text _statusText;

        [Header("Player Count Selection")]
        [SerializeField] private GameObject _playerCountPanel;
        [SerializeField] private Button _twoPlayerButton;
        [SerializeField] private Button _threePlayerButton;
        [SerializeField] private Button _fourPlayerButton;

        private GameState _gameState;
        private HandView[] _allHandViews;
        private HandView[] _handViewsBySeat;

        private void Start()
        {
            _allHandViews = new[] { _bottomHandView, _rightHandView, _topHandView, _leftHandView };

            // ゲーム開始前は全座席・「次のターン」ボタンを隠しておく。
            SetActiveIfNotNull(_bottomHandView, false);
            SetActiveIfNotNull(_rightHandView, false);
            SetActiveIfNotNull(_topHandView, false);
            SetActiveIfNotNull(_leftHandView, false);

            if (_nextTurnButton != null)
            {
                _nextTurnButton.onClick.AddListener(OnNextTurnButtonClicked);
                _nextTurnButton.gameObject.SetActive(false);
            }

            if (_twoPlayerButton != null) _twoPlayerButton.onClick.AddListener(() => StartGame(2));
            if (_threePlayerButton != null) _threePlayerButton.onClick.AddListener(() => StartGame(3));
            if (_fourPlayerButton != null) _fourPlayerButton.onClick.AddListener(() => StartGame(4));

            ShowPlayerCountPanel(true);
            SetStatusText("プレイ人数を選択してください。");
        }

        /// <summary>
        /// 人数選択パネルのボタンが押されたときに呼ばれる。選択された人数でゲームを初期化する。
        /// </summary>
        private void StartGame(int playerCount)
        {
            ShowPlayerCountPanel(false);

            // 人数ごとの座席対応（基本設計書3.2節、時計回りの手番順を維持する）:
            //   2人: SeatIndex 0=Bottom, 1=Top（Right/Left は使わない）
            //   3人: SeatIndex 0=Bottom, 1=Right, 2=Left（Top は使わない）
            //   4人: SeatIndex 0=Bottom, 1=Right, 2=Top, 3=Left
            _handViewsBySeat = GetSeatMapping(playerCount);

            // 使わない座席の HandView は非表示にする（空のコンテナが見えたままだと紛らわしいため）。
            foreach (var handView in _allHandViews)
            {
                bool isUsed = Array.IndexOf(_handViewsBySeat, handView) >= 0;
                SetActiveIfNotNull(handView, isUsed);
            }

            var setupResult = GameSetup.SetUp(playerCount);
            var rouletteSelector = new RouletteSelector();
            _gameState = new GameState(setupResult, rouletteSelector, startingSeatIndex: 0);

            foreach (var player in _gameState.Players)
            {
                GetHandView(player.SeatIndex)?.SetHand(player.Hand);
            }

            if (_nextTurnButton != null)
            {
                _nextTurnButton.gameObject.SetActive(true);
                _nextTurnButton.interactable = true;
            }

            SetStatusText($"ゲーム開始（{playerCount}人）。「次のターン」を押して進行してください。");
        }

        /// <summary>
        /// SeatIndex(0始まり) → HandView の対応表を、選択された人数に応じて構築する。
        /// 配列の添字がそのまま SeatIndex に対応する。
        /// </summary>
        private HandView[] GetSeatMapping(int playerCount)
        {
            switch (playerCount)
            {
                case 2:
                    // SeatIndex 0=Bottom, 1=Top（Right/Left は使わない）
                    return new[] { _bottomHandView, _topHandView };
                case 3:
                    // SeatIndex 0=Bottom, 1=Right, 2=Left（Top は使わない）
                    return new[] { _bottomHandView, _rightHandView, _leftHandView };
                case 4:
                default:
                    // SeatIndex 0=Bottom, 1=Right, 2=Top, 3=Left
                    return new[] { _bottomHandView, _rightHandView, _topHandView, _leftHandView };
            }
        }

        private void OnNextTurnButtonClicked()
        {
            if (_gameState == null || _gameState.IsGameOver)
            {
                return;
            }

            var result = _gameState.PlayTurn();

            // 手札が変化するのは手番プレイヤーの座席のみなので、そこだけ更新すればよい。
            GetHandView(result.TurnPlayer.SeatIndex)?.SetHand(result.TurnPlayer.Hand);

            var message = BuildTurnDescription(result);

            if (_gameState.IsGameOver)
            {
                message += "\n\n" + BuildFinalRankingText();
                SetButtonInteractable(false);
            }

            SetStatusText(message);
        }

        private HandView GetHandView(int seatIndex)
        {
            if (_handViewsBySeat == null || seatIndex < 0 || seatIndex >= _handViewsBySeat.Length)
            {
                return null;
            }

            return _handViewsBySeat[seatIndex];
        }

        private static string BuildTurnDescription(TurnResult result)
        {
            var sb = new StringBuilder();
            sb.Append("Player ").Append(result.TurnPlayer.SeatIndex).Append("の番: ")
              .Append(CategoryToJapanese(result.Category)).Append("判定、基準カード")
              .Append(CardView.FormatCardText(result.DrawnCard));

            if (result.WasPass)
            {
                sb.Append("、該当なしのためパス");
            }
            else
            {
                sb.Append("、")
                  .Append(string.Join("・", result.DiscardedCards.Select(CardView.FormatCardText)))
                  .Append("を捨てた");
            }

            if (result.FlipDeckWasReconstituted)
            {
                sb.Append("（めくり札を捨て札から再構築）");
            }

            if (result.FinishedPlayer != null)
            {
                sb.Append("\nPlayer ").Append(result.FinishedPlayer.SeatIndex)
                  .Append(" が ").Append(result.FinishedPlayer.Rank).Append("位 であがりました！");
            }

            return sb.ToString();
        }

        private string BuildFinalRankingText()
        {
            var sb = new StringBuilder("=== ゲーム終了 ===");
            foreach (var player in _gameState.Rankings)
            {
                sb.Append('\n').Append(player.Rank).Append("位: Player ").Append(player.SeatIndex);
            }

            return sb.ToString();
        }

        private static string CategoryToJapanese(RouletteCategory category)
        {
            switch (category)
            {
                case RouletteCategory.Number:
                    return "数字";
                case RouletteCategory.Suit:
                    return "マーク";
                case RouletteCategory.Color:
                    return "色";
                default:
                    return category.ToString();
            }
        }

        private void SetStatusText(string message)
        {
            if (_statusText != null)
            {
                _statusText.text = message;
            }
        }

        private void SetButtonInteractable(bool interactable)
        {
            if (_nextTurnButton != null)
            {
                _nextTurnButton.interactable = interactable;
            }
        }

        private void ShowPlayerCountPanel(bool show)
        {
            if (_playerCountPanel != null)
            {
                _playerCountPanel.SetActive(show);
            }
        }

        private static void SetActiveIfNotNull(HandView handView, bool active)
        {
            if (handView != null)
            {
                handView.gameObject.SetActive(active);
            }
        }
    }
}
