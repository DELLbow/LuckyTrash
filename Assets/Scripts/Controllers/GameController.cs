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
    /// 「人数選択 → 開始プレイヤー決定 → ゲーム開始」の順で進行する。
    /// 開始プレイヤーは、前回と同じ人数・同じ座席構成であれば <see cref="GameManager"/> に
    /// 記録された前回の最下位座席から、そうでなければランダムに決定する（じゃんけんパネルは廃止）。
    /// 選択後は GameSetup でゲームを初期化し、GameState を介してターンを進行しながら、
    /// 座席の HandView と CenterArea のステータス表示・「次のターン」ボタンを繋ぐ。
    /// ゲーム終了時には、次回の開始プレイヤー決定用に最下位座席を GameManager に記録し、
    /// 簡易な「もう一度プレイ」ボタンで人数選択からやり直せるようにしている。
    /// ResultScene への遷移は今回のスコープ外で、ここでは GameScene 内で完結させている。
    /// </summary>
    public class GameController : MonoBehaviour
    {
        // 4座席分の HandView（基本設計書3.2節: Bottom/Right/Top/Left）。
        [SerializeField] private HandView _bottomHandView;
        [SerializeField] private HandView _rightHandView;
        [SerializeField] private HandView _topHandView;
        [SerializeField] private HandView _leftHandView;

        [SerializeField] private Button _nextTurnButton;
        [SerializeField] private Button _playAgainButton;
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

            if (_nextTurnButton != null)
            {
                _nextTurnButton.onClick.AddListener(OnNextTurnButtonClicked);
            }

            if (_playAgainButton != null)
            {
                _playAgainButton.onClick.AddListener(OnPlayAgainButtonClicked);
            }

            if (_twoPlayerButton != null) _twoPlayerButton.onClick.AddListener(() => OnPlayerCountSelected(2));
            if (_threePlayerButton != null) _threePlayerButton.onClick.AddListener(() => OnPlayerCountSelected(3));
            if (_fourPlayerButton != null) _fourPlayerButton.onClick.AddListener(() => OnPlayerCountSelected(4));

            ResetToPlayerCountSelection();
        }

        /// <summary>
        /// 人数選択画面（初回起動時・「もう一度プレイ」時）の状態に戻す。
        /// </summary>
        private void ResetToPlayerCountSelection()
        {
            _gameState = null;

            foreach (var handView in _allHandViews)
            {
                SetActiveIfNotNull(handView, false);
            }

            if (_nextTurnButton != null)
            {
                _nextTurnButton.gameObject.SetActive(false);
            }

            if (_playAgainButton != null)
            {
                _playAgainButton.gameObject.SetActive(false);
            }

            ShowPlayerCountPanel(true);
            SetStatusText("プレイ人数を選択してください。");
        }

        /// <summary>
        /// 人数選択パネルのボタンが押されたときに呼ばれる。
        /// 選択人数に応じた座席対応表を確定させ、開始プレイヤーを決定してゲームを開始する。
        /// 前回と同じ人数・同じ座席構成なら前回の最下位座席から、そうでなければランダムに決定する。
        /// </summary>
        private void OnPlayerCountSelected(int playerCount)
        {
            ShowPlayerCountPanel(false);

            // 人数ごとの座席対応（基本設計書3.2節、時計回りの手番順を維持する）:
            //   2人: SeatIndex 0=Bottom, 1=Top（Right/Left は使わない）
            //   3人: SeatIndex 0=Bottom, 1=Right, 2=Left（Top は使わない）
            //   4人: SeatIndex 0=Bottom, 1=Right, 2=Top, 3=Left
            _handViewsBySeat = GetSeatMapping(playerCount);

            var seatIndices = Enumerable.Range(0, _handViewsBySeat.Length).ToArray();

            int startingSeatIndex;
            string startReason;
            if (GameManager.Instance.TryGetStartingSeat(playerCount, seatIndices, out startingSeatIndex))
            {
                startReason = "（前回最下位だった座席から開始）";
            }
            else
            {
                startingSeatIndex = UnityEngine.Random.Range(0, seatIndices.Length);
                startReason = "（開始プレイヤーをランダムに決定）";
            }

            BeginGame(playerCount, startingSeatIndex, startReason);
        }

        /// <summary>
        /// 選択された人数・開始プレイヤーで実際にゲームを初期化して開始する。
        /// </summary>
        private void BeginGame(int playerCount, int startingSeatIndex, string startReason)
        {
            // 使わない座席の HandView は非表示にする（空のコンテナが見えたままだと紛らわしいため）。
            foreach (var handView in _allHandViews)
            {
                bool isUsed = Array.IndexOf(_handViewsBySeat, handView) >= 0;
                SetActiveIfNotNull(handView, isUsed);
            }

            var setupResult = GameSetup.SetUp(playerCount);
            var rouletteSelector = new RouletteSelector();
            _gameState = new GameState(setupResult, rouletteSelector, startingSeatIndex);

            foreach (var player in _gameState.Players)
            {
                GetHandView(player.SeatIndex)?.SetHand(player.Hand);
            }

            if (_nextTurnButton != null)
            {
                _nextTurnButton.gameObject.SetActive(true);
                _nextTurnButton.interactable = true;
            }

            SetStatusText($"ゲーム開始（{playerCount}人）。Player {startingSeatIndex} から開始します{startReason}。\n「次のターン」を押して進行してください。");
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

                if (_nextTurnButton != null)
                {
                    _nextTurnButton.gameObject.SetActive(false);
                }

                // 残り1人になり自動的に最下位が確定した座席を、次回の開始プレイヤー決定用に記録する。
                RecordGameResultForNextStart();

                if (_playAgainButton != null)
                {
                    _playAgainButton.gameObject.SetActive(true);
                }
            }

            SetStatusText(message);
        }

        /// <summary>
        /// ゲーム終了時、最後まで手札を持っていた（自動的に最下位が確定した）プレイヤーの座席を、
        /// 今回のプレイ人数・座席構成とあわせて GameManager に記録する。
        /// </summary>
        private void RecordGameResultForNextStart()
        {
            if (_gameState == null || _handViewsBySeat == null)
            {
                return;
            }

            var rankings = _gameState.Rankings;
            if (rankings.Count == 0)
            {
                return;
            }

            // 最下位（最も大きい順位番号）＝残り1人になり自動的に確定したプレイヤー。
            int lastPlaceSeatIndex = rankings[rankings.Count - 1].SeatIndex;
            int playerCount = _handViewsBySeat.Length;
            var seatIndices = Enumerable.Range(0, playerCount);

            GameManager.Instance.RecordGameResult(playerCount, seatIndices, lastPlaceSeatIndex);
        }

        /// <summary>
        /// 「もう一度プレイ」ボタンが押されたときに呼ばれる。人数選択画面に戻る。
        /// </summary>
        private void OnPlayAgainButtonClicked()
        {
            ResetToPlayerCountSelection();
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
