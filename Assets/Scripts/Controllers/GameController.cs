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
    /// GameSetup でゲームを初期化し、GameState を介してターンを進行しながら、
    /// 4座席の HandView と CenterArea のステータス表示・「次のターン」ボタンを繋ぐ。
    /// ResultScene への遷移や GameManager シングルトン化は次のステップで対応する想定で、
    /// ここでは GameScene 内で完結させている。
    /// </summary>
    public class GameController : MonoBehaviour
    {
        private const int PlayerCount = 4;

        // 座席順（SeatIndex 0=Bottom, 1=Right, 2=Top, 3=Left、基本設計書3.2節）に対応する HandView。
        [SerializeField] private HandView _bottomHandView;
        [SerializeField] private HandView _rightHandView;
        [SerializeField] private HandView _topHandView;
        [SerializeField] private HandView _leftHandView;

        [SerializeField] private Button _nextTurnButton;
        [SerializeField] private TMP_Text _statusText;

        private GameState _gameState;
        private HandView[] _handViewsBySeat;

        private void Start()
        {
            _handViewsBySeat = new[] { _bottomHandView, _rightHandView, _topHandView, _leftHandView };

            var setupResult = GameSetup.SetUp(PlayerCount);
            var rouletteSelector = new RouletteSelector();
            _gameState = new GameState(setupResult, rouletteSelector, startingSeatIndex: 0);

            foreach (var player in _gameState.Players)
            {
                GetHandView(player.SeatIndex)?.SetHand(player.Hand);
            }

            if (_nextTurnButton != null)
            {
                _nextTurnButton.onClick.AddListener(OnNextTurnButtonClicked);
            }

            SetStatusText("ゲーム開始（4人）。「次のターン」を押して進行してください。");
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
    }
}
