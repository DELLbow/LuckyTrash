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
    /// 起動時に <see cref="GameManager.TryConsumeRequestedPlayerCount"/> で TitleScene の
    /// 人数選択ポップアップからのリクエストがあれば、その人数で人数選択パネルを出さずに
    /// そのままゲームを開始する。次に <see cref="GameManager.TryConsumeQuickRestart"/> で
    /// 「クイック再開」の予約があれば、人数選択パネルを出さずに直前と同じ人数でそのまま
    /// ゲームを開始する（ResultScene の「もう一度プレイ」から戻ってきた場合）。
    /// どちらの予約も無ければ、この GameScene 内蔵の人数選択パネルを表示する
    /// （TitleScene を経由しない開発時の直接起動などのフォールバック用）。
    ///
    /// 1ターンは「ルーレットを回す」→「1枚引く」の順に固定された2段階制。ターン開始時は
    /// 「ルーレットを回す」のみ表示・有効化されており、その演出が完了すると「ルーレットを回す」を
    /// 隠して「1枚引く」を表示・有効化する。「1枚引く」の演出が完了すると両ボタンとも隠し、
    /// GameState.ResolveTurn() で実際の判定・手札更新を行う。結果ポップアップの登場〜退場が
    /// 終わったら自動的に次のプレイヤーのターンへ進む。ゲーム終了時には、最終順位・最下位座席を
    /// GameManager に記録したうえで ResultScene へ遷移する（結果表示はここでは行わない）。
    ///
    /// 下座席（SeatIndex 0）は常に人間、それ以外はCPUとして扱う（CPU対戦、判断ロジックは持たない
    /// 演出上の自動化のみ）。CPUの手番になると「1枚引く」「ルーレットを回す」ボタンを非活性化し、
    /// 人間と同じ固定順（ルーレット→ドロー）で、各アクション前にランダムな待機
    /// （<see cref="_cpuActionDelayMin"/>〜<see cref="_cpuActionDelayMax"/>）を挟みながら、
    /// 既存の2アクションのコルーチンをそのまま自動実行する（演出・判定ロジックは無改変）。
    /// </summary>
    public class GameController : MonoBehaviour
    {
        private const string ResultSceneName = "ResultScene";
        private const int HumanSeatIndex = 0;

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

        [Header("Camera")]
        [Tooltip("ターン進行に合わせたカメラ演出を担当する。未設定の場合はカメラ演出なしで従来通り動作する。")]
        [SerializeField] private CameraDirector _cameraDirector;

        [Header("Debug")]
        [SerializeField] private TMP_Text _statusText;
        [Tooltip("ONの場合のみ、「Player Xの番: マーク判定...」のような詳細な判定結果テキストを表示する。")]
        [SerializeField] private bool _showDebugResultText = false;

        [Header("Turn Indicator")]
        [Tooltip("「CPU 2の番です」のような、現在の手番プレイヤーを示す常時表示。")]
        [SerializeField] private TMP_Text _turnIndicatorText;

        [Header("CPU Turn")]
        [Tooltip("CPUの手番で、1つ目のアクションを実行するまでのランダム待機時間（秒）の最小値。")]
        [SerializeField] private float _cpuActionDelayMin = 0.5f;
        [Tooltip("CPUの手番で、各アクションを実行するまでのランダム待機時間（秒）の最大値。")]
        [SerializeField] private float _cpuActionDelayMax = 1f;

        [Header("Player Count Selection")]
        [SerializeField] private GameObject _playerCountPanel;
        [SerializeField] private Button _twoPlayerButton;
        [SerializeField] private Button _threePlayerButton;
        [SerializeField] private Button _fourPlayerButton;

        /// <summary>
        /// 1ターン内の進行段階（ルーレット待ち → ドロー待ち → 判定中）。
        /// 「どちらを先に押したか」の分岐は不要になったため、単純な3段階の状態遷移で表現する。
        /// </summary>
        private enum TurnPhase
        {
            WaitingForSpin,
            WaitingForDraw,
            Resolving
        }

        private GameState _gameState;
        private HandView[] _allHandViews;
        private HandView[] _handViewsBySeat;

        private TurnPhase _turnPhase;
        private bool _isHumanTurn;

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

            if (GameManager.Instance.TryConsumeRequestedPlayerCount(out int requestedPlayerCount))
            {
                // TitleScene の人数選択ポップアップ（CPU対戦）から遷移してきた場合:
                // 人数選択を経ずに、指定された人数でそのまま開始する。
                ShowPlayerCountPanel(false);
                OnPlayerCountSelected(requestedPlayerCount);
            }
            else if (GameManager.Instance.TryConsumeQuickRestart(out int quickRestartPlayerCount))
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

            string humanDisplayName = NameInputPopupView.LoadPlayerName();
            var setupResult = GameSetup.SetUp(playerCount, humanSeatIndex: HumanSeatIndex, humanDisplayName: humanDisplayName);
            var rouletteSelector = new RouletteSelector();
            _gameState = new GameState(setupResult, rouletteSelector, startingSeatIndex);

            foreach (var player in _gameState.Players)
            {
                var handView = GetHandView(player.SeatIndex);
                handView?.SetHand(player.Hand, faceDown: !player.IsHuman);
                handView?.SetPlayerLabel(GetDisplayName(player));
            }

            SetStatusText($"ゲーム開始（{playerCount}人）。{GetDisplayName(_gameState.CurrentPlayer)} から開始します{startReason}。");

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
        // 「ルーレットを回す」→「1枚引く」の順に固定された2段階制。
        // --------------------------------------------------------------

        private void OnSpinRouletteButtonClicked()
        {
            if (_gameState == null || _gameState.IsGameOver || _turnPhase != TurnPhase.WaitingForSpin)
            {
                return;
            }

            if (_spinRouletteButton != null)
            {
                _spinRouletteButton.interactable = false;
            }

            StartCoroutine(SpinRouletteRoutine());
        }

        /// <summary>
        /// カテゴリを抽選してから（結果を先に確定させないと、どの扇形で止めればよいか分からないため）、
        /// ルーレットの回転演出を再生する。完了後、「ルーレットを回す」を隠して「1枚引く」に切り替える。
        /// </summary>
        private IEnumerator SpinRouletteRoutine()
        {
            // 演出1: ボタン押下と同時にカメラをルーレットへ寄せる（waitForCameraArrivalがONの場合のみ、
            // 到着まで以降の処理をブロックする）。
            if (_cameraDirector != null)
            {
                yield return StartCoroutine(_cameraDirector.FocusRoulette());
            }

            _pendingCategory = _gameState.SpinCategory();

            if (_rouletteWheelView != null)
            {
                yield return StartCoroutine(_rouletteWheelView.SpinTo(_pendingCategory));
            }

            // 回転が停止し結果が確定したら、定位置へ戻す（後続のドロー待ちを止めないよう待機しない）。
            if (_cameraDirector != null)
            {
                _cameraDirector.ReturnFromRoulette();
            }

            _turnPhase = TurnPhase.WaitingForDraw;
            ShowDrawPhase();
        }

        private void OnDrawCardButtonClicked()
        {
            if (_gameState == null || _gameState.IsGameOver || _turnPhase != TurnPhase.WaitingForDraw)
            {
                return;
            }

            if (_drawCardButton != null)
            {
                _drawCardButton.interactable = false;
            }

            StartCoroutine(DrawCardRoutine());
        }

        /// <summary>
        /// 実際に GameState.DrawReferenceCard() を先に呼んでカードを確定させたうえで、
        /// 「1枚引く」の演出（山札→基準カードスロットへのスライド+フリップ）を再生する。
        /// カードを先に確定させておくことで、演出開始前（裏向きで山札から出てくる瞬間）に
        /// 正しいマテリアルを設定でき、以後は差し替え不要になる（両面シェーダーが回転に応じて
        /// 裏/表を自動的に見せる）。演出完了後、両ボタンを隠し、判定処理（ResolveTurnRoutine）を
        /// 開始する。
        /// </summary>
        private IEnumerator DrawCardRoutine()
        {
            // 演出2: ボタン押下と同時にカメラを山札・基準カードスロットへ寄せる。
            if (_cameraDirector != null)
            {
                yield return StartCoroutine(_cameraDirector.FocusDraw());
            }

            var (card, reconstituted) = _gameState.DrawReferenceCard();
            _pendingDrawnCard = card;
            _pendingFlipDeckWasReconstituted = reconstituted;
            UpdateDeckCountLabel();

            // 演出6: めくり札用デッキが尽きて再構築された場合、演出2のフォーカスをさらに山札へ
            // 寄せ直す（進行中の移動を中断して滑らかに繋ぐ、CameraDirector側の設計により実現）。
            if (reconstituted && _cameraDirector != null)
            {
                yield return StartCoroutine(_cameraDirector.FocusReshuffle());
            }

            if (_referenceCardDrawView != null)
            {
                yield return StartCoroutine(_referenceCardDrawView.PlayDrawAnimation(card));
            }

            // ドロー完了後、定位置へ戻す（後続の判定処理を止めないよう待機しない）。
            if (_cameraDirector != null)
            {
                _cameraDirector.ReturnFromDraw();
            }

            _turnPhase = TurnPhase.Resolving;
            HideTurnActionButtons();
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
            GetHandView(result.TurnPlayer.SeatIndex)?.SetHand(result.TurnPlayer.Hand, faceDown: !result.TurnPlayer.IsHuman);

            if (_statusText != null)
            {
                _statusText.text = _showDebugResultText ? BuildTurnDescription(result) : string.Empty;
            }

            bool playerFinished = result.FinishedPlayer != null;

            // 演出4: 手札が0枚になった座席へズームイン。
            if (playerFinished && _cameraDirector != null)
            {
                yield return StartCoroutine(_cameraDirector.FocusFinish(GetSeatSide(result.FinishedPlayer.SeatIndex)));
            }

            if (_resultPopupView != null)
            {
                yield return StartCoroutine(_resultPopupView.ShowResult(result.DiscardedCards.Count));
            }

            // あがったターンは必ず1枚以上捨てているため、上のトラッシュ枚数ポップアップと
            // 「あがり!」ポップアップが連続して発生する。2つが重ならないよう、トラッシュ表示の
            // 退場が完全に終わってから（上のyieldが完了してから）あがり表示を始める。
            if (playerFinished && _resultPopupView != null)
            {
                string rankText = result.FinishedPlayer.Rank.HasValue
                    ? $"{result.FinishedPlayer.Rank}位あがり!"
                    : "あがり!";
                yield return StartCoroutine(_resultPopupView.ShowMessage(rankText));
            }

            if (result.GameEnded)
            {
                // 残り1人になり自動的に最下位が確定した座席・最終順位を、
                // 次回の開始プレイヤー決定・結果表示用に記録してから ResultScene へ遷移する。
                // GameManagerへの記録はここで同期的に完了するため、演出5のズームアウトで
                // 遷移を数秒遅らせても結果データの受け渡しには影響しない。
                RecordGameResultForNextStart();

                // 演出5: シーン遷移前にテーブル全体を見せるズームアウト（waitForCameraArrivalの
                // 設定に関わらず、常に最後まで見せてから遷移する）。
                if (_cameraDirector != null)
                {
                    yield return StartCoroutine(_cameraDirector.ZoomOutForGameEnd());
                }

                SceneManager.LoadScene(ResultSceneName);
                yield break;
            }

            StartNextTurn();
        }

        /// <summary>
        /// 次のプレイヤーのターンを始められる状態に戻す
        /// （「ルーレットを回す」のみ表示・有効化、基準カードスロットのクリア、山札枚数ラベル更新、
        /// ターン表示更新）。手番が人間なら通常通り操作を待ち、CPUならCPU自動進行を開始する。
        /// </summary>
        private void StartNextTurn()
        {
            _turnPhase = TurnPhase.WaitingForSpin;

            if (_referenceCardDrawView != null)
            {
                _referenceCardDrawView.ClearAndReset();
            }

            UpdateDeckCountLabel();
            UpdateTurnIndicator();

            // 人間の手番のみボタンを操作可能にする。CPUの手番は非活性のまま自動進行させる。
            _isHumanTurn = _gameState != null
                && !_gameState.IsGameOver
                && _gameState.CurrentPlayer != null
                && _gameState.CurrentPlayer.IsHuman;

            // 演出3: ターン開始時、そのプレイヤーの座席側へ軽くパンする。CPU/人間を問わず毎ターン発生する。
            if (_cameraDirector != null && _gameState != null && _gameState.CurrentPlayer != null)
            {
                _cameraDirector.PanToTurn(GetSeatSide(_gameState.CurrentPlayer.SeatIndex));
            }

            ShowSpinPhase();

            if (!_isHumanTurn && _gameState != null && !_gameState.IsGameOver)
            {
                StartCoroutine(CpuTurnRoutine());
            }
        }

        /// <summary>
        /// 「ルーレットを回す」のみ表示・有効化し、「1枚引く」を隠す（ターン開始時の状態）。
        /// </summary>
        private void ShowSpinPhase()
        {
            if (_spinRouletteButton != null)
            {
                _spinRouletteButton.gameObject.SetActive(true);
                _spinRouletteButton.interactable = _isHumanTurn;
            }

            if (_drawCardButton != null)
            {
                _drawCardButton.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 「1枚引く」のみ表示・有効化し、「ルーレットを回す」を隠す（ルーレット完了後の状態）。
        /// </summary>
        private void ShowDrawPhase()
        {
            if (_spinRouletteButton != null)
            {
                _spinRouletteButton.gameObject.SetActive(false);
            }

            if (_drawCardButton != null)
            {
                _drawCardButton.gameObject.SetActive(true);
                _drawCardButton.interactable = _isHumanTurn;
            }
        }

        /// <summary>
        /// 両ボタンとも隠す（ドロー完了〜判定処理中の状態）。
        /// </summary>
        private void HideTurnActionButtons()
        {
            if (_spinRouletteButton != null) _spinRouletteButton.gameObject.SetActive(false);
            if (_drawCardButton != null) _drawCardButton.gameObject.SetActive(false);
        }

        // --------------------------------------------------------------
        // CPUターンの自動進行。判断ロジックは持たず、人間と同じ固定順（ルーレット→ドロー）で
        // 既存の2アクションのコルーチンをランダムな待機を挟んで自動実行するだけの演出。
        // --------------------------------------------------------------

        /// <summary>
        /// CPUの手番を自動進行する。ルーレット→ドローの固定順で、それぞれの前にランダムな
        /// 待機を挟んでから、既存の演出付きコルーチンをそのまま実行する。
        /// </summary>
        private IEnumerator CpuTurnRoutine()
        {
            yield return StartCoroutine(WaitRandomCpuDelay());
            yield return StartCoroutine(SpinRouletteRoutine());

            yield return StartCoroutine(WaitRandomCpuDelay());
            yield return StartCoroutine(DrawCardRoutine());
        }

        private IEnumerator WaitRandomCpuDelay()
        {
            float delay = UnityEngine.Random.Range(_cpuActionDelayMin, _cpuActionDelayMax);
            yield return new WaitForSeconds(delay);
        }

        /// <summary>
        /// 「CPU 2の番です」のような、現在の手番プレイヤーを示す常時表示を更新する。
        /// </summary>
        private void UpdateTurnIndicator()
        {
            if (_turnIndicatorText == null || _gameState == null || _gameState.CurrentPlayer == null)
            {
                return;
            }

            _turnIndicatorText.text = $"{GetDisplayName(_gameState.CurrentPlayer)}の番です";
        }

        /// <summary>
        /// プレイヤーの表示名を取得する（<see cref="Player.DisplayName"/> が未設定の場合のフォールバック付き）。
        /// </summary>
        private static string GetDisplayName(Player player)
        {
            return !string.IsNullOrEmpty(player.DisplayName) ? player.DisplayName : $"Player {player.SeatIndex}";
        }

        private void UpdateDeckCountLabel()
        {
            if (_deckCountText != null && _gameState != null)
            {
                _deckCountText.text = $"残り{_gameState.FlipDeckCount}枚";
            }
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

        /// <summary>
        /// SeatIndex を、CameraDirector が使う画面上の座席方向（Bottom/Right/Top/Left）に変換する。
        /// 人数ごとの座席対応（<see cref="GetSeatMapping"/>）に関わらず、実際にどの HandView
        /// （＝どの3Dアンカー）が使われているかで判定するため、2人/3人/4人のどの構成でも正しく解決する。
        /// </summary>
        private CameraDirector.SeatSide GetSeatSide(int seatIndex)
        {
            var handView = GetHandView(seatIndex);

            if (handView == _rightHandView) return CameraDirector.SeatSide.Right;
            if (handView == _topHandView) return CameraDirector.SeatSide.Top;
            if (handView == _leftHandView) return CameraDirector.SeatSide.Left;
            return CameraDirector.SeatSide.Bottom;
        }

        private static string BuildTurnDescription(TurnResult result)
        {
            var sb = new StringBuilder();
            sb.Append(GetDisplayName(result.TurnPlayer)).Append("の番: ")
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
                sb.Append('\n').Append(GetDisplayName(result.FinishedPlayer))
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
