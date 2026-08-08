using System;
using System.Collections;
using System.Linq;
using System.Text;
using LuckyTrash.Cards;
using LuckyTrash.Game;
using LuckyTrash.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LuckyTrash.Controllers
{
    /// <summary>
    /// GameScene に配置し、ゲーム進行全体を統括する開発用コントローラー。
    /// 「人数選択 → 開始プレイヤー決定 → ゲーム開始」の順で進行する。
    /// 開始プレイヤーは、前回と同じ人数・同じ座席構成であれば <see cref="GameManager"/> に
    /// 記録された前回の最下位座席から、そうでなければランダムに決定する。
    /// 起動時に <see cref="GameManager.TryConsumeQuickRestart"/> で「クイック再開」の予約が
    /// あれば、人数選択パネルを出さずに直前と同じ人数でそのままゲームを開始する
    /// （ResultScene の「もう一度プレイ」から戻ってきた場合）。
    ///
    /// 1ターンは「1枚引く」「ルーレットを回す」の2ボタン制。プレイヤーはどちらを先に
    /// 押してもよく、押した方のボタンはその場で非活性化される。両方のアクション（と、
    /// それぞれの演出）が完了して初めて GameState.ResolveTurn() で実際の判定・手札更新を行い、
    /// 結果ポップアップの登場〜退場が終わったら自動的に次のプレイヤーのターンへ進む。
    /// ゲーム終了時には、最終順位・最下位座席を GameManager に記録したうえで
    /// ResultScene へ遷移する（結果表示はここでは行わない）。
    /// </summary>
    public class GameController : MonoBehaviour
    {
        private const string ResultSceneName = "ResultScene";

        // 4座席分の HandView（基本設計書3.2節: Bottom/Right/Top/Left）。
        [SerializeField] private HandView _bottomHandView;
        [SerializeField] private HandView _rightHandView;
        [SerializeField] private HandView _topHandView;
        [SerializeField] private HandView _leftHandView;

        [Header("Turn Actions")]
        [SerializeField] private Button _drawCardButton;
        [SerializeField] private Button _spinRouletteButton;
        [SerializeField] private RouletteWheelView _rouletteWheelView;
        [SerializeField] private ReferenceCardDrawView _referenceCardDrawView;
        [SerializeField] private ResultPopupView _resultPopupView;
        [SerializeField] private TMP_Text _deckCountText;

        [Header("Debug")]
        [SerializeField] private TMP_Text _statusText;
        [Tooltip("ONの場合のみ、「Player Xの番: マーク判定...」のような詳細な判定結果テキストを表示する。")]
        [SerializeField] private bool _showDebugResultText = false;

        [Header("Player Count Selection")]
        [SerializeField] private GameObject _playerCountPanel;
        [SerializeField] private Button _twoPlayerButton;
        [SerializeField] private Button _threePlayerButton;
        [SerializeField] private Button _fourPlayerButton;

        private GameState _gameState;
        private HandView[] _allHandViews;
        private HandView[] _handViewsBySeat;

        // 「1枚引く」「ルーレットを回す」それぞれの進行状況。両方 true になったら判定へ進む。
        private bool _drawInProgress;
        private bool _spinInProgress;
        private bool _drawActionDone;
        private bool _spinActionDone;
        private bool _isResolvingTurn;

        // 両アクション完了後に GameState.ResolveTurn() へ渡すための、確定済みの抽選結果。
        private RouletteCategory _pendingCategory;
        private Card _pendingDrawnCard;
        private bool _pendingFlipDeckWasReconstituted;

        private void Start()
        {
            _allHandViews = new[] { _bottomHandView, _rightHandView, _topHandView, _leftHandView };

            foreach (var handView in _allHandViews)
            {
                SetActiveIfNotNull(handView, false);
            }

            if (_drawCardButton != null)
            {
                _drawCardButton.onClick.AddListener(OnDrawCardButtonClicked);
                _drawCardButton.gameObject.SetActive(false);
            }

            if (_spinRouletteButton != null)
            {
                _spinRouletteButton.onClick.AddListener(OnSpinRouletteButtonClicked);
                _spinRouletteButton.gameObject.SetActive(false);
            }

            if (_twoPlayerButton != null) _twoPlayerButton.onClick.AddListener(() => OnPlayerCountSelected(2));
            if (_threePlayerButton != null) _threePlayerButton.onClick.AddListener(() => OnPlayerCountSelected(3));
            if (_fourPlayerButton != null) _fourPlayerButton.onClick.AddListener(() => OnPlayerCountSelected(4));

            if (GameManager.Instance.TryConsumeQuickRestart(out int quickRestartPlayerCount))
            {
                // ResultScene の「もう一度プレイ」から戻ってきた場合: 人数選択を経ずにそのまま開始する。
                ShowPlayerCountPanel(false);
                OnPlayerCountSelected(quickRestartPlayerCount);
            }
            else
            {
                ShowPlayerCountPanel(true);
                SetStatusText("プレイ人数を選択してください。");
            }
        }

        /// <summary>
        /// 人数選択パネルのボタンが押されたとき、またはクイック再開時に呼ばれる。
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

            SetStatusText($"ゲーム開始（{playerCount}人）。Player {startingSeatIndex} から開始します{startReason}。");

            StartNextTurn();
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

        // --------------------------------------------------------------
        // 「1枚引く」「ルーレットを回す」の2ボタン制。
        // --------------------------------------------------------------

        private void OnDrawCardButtonClicked()
        {
            if (_gameState == null || _gameState.IsGameOver || _drawActionDone || _drawInProgress)
            {
                return;
            }

            _drawInProgress = true;
            if (_drawCardButton != null)
            {
                _drawCardButton.interactable = false;
            }

            StartCoroutine(DrawCardRoutine());
        }

        /// <summary>
        /// 「1枚引く」の演出（山札→基準カードスロットへのスライド+フリップ）を再生し、
        /// 完了後に実際に GameState.DrawReferenceCard() を呼んでカードを確定・表示する。
        /// </summary>
        private IEnumerator DrawCardRoutine()
        {
            if (_referenceCardDrawView != null)
            {
                yield return StartCoroutine(_referenceCardDrawView.PlayDrawAnimation());
            }

            var (card, reconstituted) = _gameState.DrawReferenceCard();
            _pendingDrawnCard = card;
            _pendingFlipDeckWasReconstituted = reconstituted;

            if (_referenceCardDrawView != null)
            {
                _referenceCardDrawView.ShowCard(card);
            }

            UpdateDeckCountLabel();

            _drawActionDone = true;
            TryResolveTurnIfBothActionsComplete();
        }

        private void OnSpinRouletteButtonClicked()
        {
            if (_gameState == null || _gameState.IsGameOver || _spinActionDone || _spinInProgress)
            {
                return;
            }

            _spinInProgress = true;
            if (_spinRouletteButton != null)
            {
                _spinRouletteButton.interactable = false;
            }

            StartCoroutine(SpinRouletteRoutine());
        }

        /// <summary>
        /// カテゴリを抽選してから（結果を先に確定させないと、どの扇形で止めればよいか分からないため）、
        /// ルーレットの回転演出を再生する。
        /// </summary>
        private IEnumerator SpinRouletteRoutine()
        {
            _pendingCategory = _gameState.SpinCategory();

            if (_rouletteWheelView != null)
            {
                yield return StartCoroutine(_rouletteWheelView.SpinTo(_pendingCategory));
            }

            _spinActionDone = true;
            TryResolveTurnIfBothActionsComplete();
        }

        /// <summary>
        /// 「1枚引く」「ルーレットを回す」の両方の演出が完了していれば、判定処理へ進む。
        /// どちらか一方だけでは何もしない。
        /// </summary>
        private void TryResolveTurnIfBothActionsComplete()
        {
            if (!_drawActionDone || !_spinActionDone || _isResolvingTurn)
            {
                return;
            }

            _isResolvingTurn = true;
            StartCoroutine(ResolveTurnRoutine());
        }

        /// <summary>
        /// 両アクション完了後、実際の判定（GameState.ResolveTurn）を行い、結果ポップアップを
        /// 表示してから、ゲーム終了なら ResultScene へ、そうでなければ次のプレイヤーのターンへ進む。
        /// </summary>
        private IEnumerator ResolveTurnRoutine()
        {
            var result = _gameState.ResolveTurn(_pendingCategory, _pendingDrawnCard, _pendingFlipDeckWasReconstituted);

            // 手札が変化するのは手番プレイヤーの座席のみなので、そこだけ更新すればよい。
            GetHandView(result.TurnPlayer.SeatIndex)?.SetHand(result.TurnPlayer.Hand);

            if (_statusText != null)
            {
                _statusText.text = _showDebugResultText ? BuildTurnDescription(result) : string.Empty;
            }

            if (_resultPopupView != null)
            {
                yield return StartCoroutine(_resultPopupView.ShowResult(result.DiscardedCards.Count));
            }

            if (result.GameEnded)
            {
                // 残り1人になり自動的に最下位が確定した座席・最終順位を、
                // 次回の開始プレイヤー決定・結果表示用に記録してから ResultScene へ遷移する。
                RecordGameResultForNextStart();
                SceneManager.LoadScene(ResultSceneName);
                yield break;
            }

            StartNextTurn();
        }

        /// <summary>
        /// 次のプレイヤーのターンを始められる状態に戻す
        /// （両ボタンの再有効化、基準カードスロットのクリア、山札枚数ラベル更新）。
        /// </summary>
        private void StartNextTurn()
        {
            _drawInProgress = false;
            _spinInProgress = false;
            _drawActionDone = false;
            _spinActionDone = false;
            _isResolvingTurn = false;

            if (_referenceCardDrawView != null)
            {
                _referenceCardDrawView.ClearAndReset();
            }

            UpdateDeckCountLabel();
            SetActionButtonsInteractable(true);

            if (_drawCardButton != null) _drawCardButton.gameObject.SetActive(true);
            if (_spinRouletteButton != null) _spinRouletteButton.gameObject.SetActive(true);
        }

        private void UpdateDeckCountLabel()
        {
            if (_deckCountText != null && _gameState != null)
            {
                _deckCountText.text = $"残り{_gameState.FlipDeckCount}枚";
            }
        }

        private void SetActionButtonsInteractable(bool interactable)
        {
            if (_drawCardButton != null) _drawCardButton.interactable = interactable;
            if (_spinRouletteButton != null) _spinRouletteButton.interactable = interactable;
        }

        // --------------------------------------------------------------
        // ゲーム終了時の記録・共通ヘルパー。
        // --------------------------------------------------------------

        /// <summary>
        /// ゲーム終了時、最後まで手札を持っていた（自動的に最下位が確定した）プレイヤーの座席と
        /// 最終順位を、今回のプレイ人数・座席構成とあわせて GameManager に記録する。
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
            var finalRankingSeatIndices = rankings.Select(p => p.SeatIndex).ToList();

            GameManager.Instance.RecordGameResult(playerCount, seatIndices, lastPlaceSeatIndex, finalRankingSeatIndices);
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
