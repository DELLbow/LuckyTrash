using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LuckyTrash.Cards;
using LuckyTrash.Game;
using LuckyTrash.UI;
using LuckyTrash.UI.Scoring;
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
        [Tooltip("画面右上の山札の残り枚数表示（カードの束型、数字がドラムのように回る）。")]
        [SerializeField] private DeckCounterView _deckCounter;

        [Header("Camera")]
        [Tooltip("ターン進行に合わせたカメラ演出を担当する。未設定の場合はカメラ演出なしで従来通り動作する。")]
        [SerializeField] private CameraDirector _cameraDirector;

        [Header("Debug")]
        [SerializeField] private TMP_Text _statusText;
        [Tooltip("ONの場合のみ、開発用のステータス表示（「ゲーム開始（4人）。〜から開始します」など）を表示する。")]
        [SerializeField] private bool _showStatusText = false;
        [Tooltip("ONの場合のみ、「Player Xの番: マーク判定...」のような詳細な判定結果テキストを表示する。")]
        [SerializeField] private bool _showDebugResultText = false;

        [Header("Turn Indicator")]
        [Tooltip("「CPU 2の番です」のような、現在の手番プレイヤーを示す常時表示。")]
        [SerializeField] private TMP_Text _turnIndicatorText;
        [Tooltip("ONの場合のみ、画面中央の「〇〇の番です」の常時表示を出す（通常は紙幣の演出とネームプレートの強調で示す）。")]
        [SerializeField] private bool _showTurnIndicatorText = false;

        [Header("Turn Banknote / Spotlight")]
        [Tooltip("手番が変わったときに、紙幣が画面を流れていく演出。")]
        [SerializeField] private TurnBanknoteView _turnBanknote;
        [Tooltip("手番の人の手札の下のフェルトを照らす演出（任意）。")]
        [SerializeField] private TurnSpotlightView _turnSpotlight;

        [Header("Finish Celebration")]
        [Tooltip("あがりの金の紙幣が中央で止まった瞬間に、小さな紙幣をはじけさせる演出。")]
        [SerializeField] private MoneyBurstView _moneyBurst;
        [Tooltip("背景のカジノのフロア。あがりの金の紙幣が中央で止まった瞬間に、スロットマシンの光を一斉に光らせる（任意）。")]
        [SerializeField] private CasinoBackgroundView _casinoBackground;

        [Header("Score")]
        [Tooltip("得点のルール（順位の得点とボーナス）。Assets/Settings/ScoreRules.asset。")]
        [SerializeField] private ScoreRulesAsset _scoreRules;

        [Header("Game End Finale")]
        [Tooltip("順位が確定したあとの最後の演出と、取引明細書（GameScene の上で見せる）。")]
        [SerializeField] private GameFinaleView _finaleView;
        [Tooltip("ONの場合のみ、以前のように ResultScene へ切り替えて結果を見せる（取引明細書は使わない）。")]
        [SerializeField] private bool _useResultScene = false;
        [Tooltip("最下位の人の残りの手札を、1枚ずつ捨て札の山へ放り込む間隔（秒）。")]
        [SerializeField] private float _finaleThrowStagger = 0.22f;
        [Tooltip("「TRASH」のスタンプを押してから、1位を祝う演出に移るまでの間（秒）。")]
        [SerializeField] private float _finalePauseAfterTrash = 0.35f;
        [Tooltip("1位の金の紙幣の大きさ（あがりのときの紙幣に対する倍率）。")]
        [SerializeField] private float _finaleChampionNoteScale = 1.3f;
        [Tooltip("1位の金の紙幣が中央で止まっている時間（秒）。")]
        [SerializeField] private float _finaleChampionHoldSeconds = 1.5f;
        [Tooltip("1位を祝うときにはじける札束の枚数（あがりのときに対する倍率）。")]
        [SerializeField] private float _finaleBurstCountScale = 2f;
        [Tooltip("1位の人のネームプレートのメダルが光っている時間（秒）。")]
        [SerializeField] private float _finaleMedalGlowSeconds = 2.0f;
        [Tooltip("1位の金の紙幣に書く文字（{0} に名前が入る）。")]
        [SerializeField] private string _finaleChampionMessage = "<size=70%>{0}</size>\n1位！";
        [Tooltip("ONの場合のみ、以前の黄色い「〇位あがり!」の表示（結果ポップアップ）を出す。通常は金の紙幣で発表する。")]
        [SerializeField] private bool _showFinishMessagePopup = false;

        [Header("Turn Result")]
        [Tooltip("盤面の捨て札の山。捨てられたカードと基準カードがここへ飛んでいく。")]
        [SerializeField] private DiscardPileView _discardPile;
        [Tooltip("ターンの結果を、画面中央に銀行のスタンプを押したように見せる演出。")]
        [SerializeField] private BankStampView _bankStamp;
        [Tooltip("ONの場合のみ、以前の黄色い「N枚トラッシュ!」の結果ポップアップで結果を見せる。")]
        [SerializeField] private bool _useLegacyResultPopup = false;
        [Tooltip("カードを捨てた後、残った手札が詰めて並び直す時間（秒）。")]
        [SerializeField] private float _handRelayoutSeconds = 0.2f;
        [Tooltip("ONの場合、カードを引いた後のカメラが定位置へ戻りきってから、手札が光り始める（スタンプも同時に押す）。")]
        [SerializeField] private bool _waitForCameraBeforeResult = true;
        [Tooltip("カメラの戻りを待つ時間の上限（秒）。何かの理由でカメラが止まらない場合でも、ここで打ち切って進める。")]
        [SerializeField] private float _cameraWaitTimeoutSeconds = 2f;

        [Header("Nameplates")]
        [Tooltip("画面左上に、参加プレイヤーのネームプレートを手番の順（1P, 2P, ...）に並べる表示。")]
        [SerializeField] private NameplateBarView _nameplateBar;

        [Header("CPU Turn")]
        [Tooltip("CPUの手番で、1つ目のアクションを実行するまでのランダム待機時間（秒）の最小値。")]
        [SerializeField] private float _cpuActionDelayMin = 0.5f;
        [Tooltip("CPUの手番で、各アクションを実行するまでのランダム待機時間（秒）の最大値。")]
        [SerializeField] private float _cpuActionDelayMax = 1f;

        [Header("Roulette Result Hold")]
        [Tooltip("ルーレット停止後、カメラが定位置へ戻り始めるまで待つ時間（秒）。この間に停止した扇形をハイライトする。人間・CPU両方の手番に適用される。")]
        [SerializeField] private float _rouletteResultHoldSeconds = 1.0f;

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
        // SeatIndex → 手番の順番（1P=1）。ネームプレートと紙幣で同じ番号・スートを使う。
        private readonly Dictionary<int, int> _turnOrderBySeat = new Dictionary<int, int>();

        private TurnPhase _turnPhase;
        private bool _isHumanTurn;

        // 「もう一度プレイ」で始まった勝負なら true（通算の得点を持ち越す）。タイトルや人数選択から始めたら false（リセット）。
        private bool _continueScoreSession;
        // 直近の勝負の得点の明細（勝負が終わったときに作る）。
        private MatchSummary _lastMatchSummary;

        /// <summary>直近の勝負の得点の明細（勝負が終わるまでは null）。</summary>
        public MatchSummary LastMatchSummary => _lastMatchSummary;

        // ゲーム終了時の最後の演出（タップで飛ばしたときに止めるコルーチン、最下位の残りの手札）。
        private readonly List<Coroutine> _finaleRoutines = new List<Coroutine>();
        private Player _finaleLastPlace;
        private List<Card> _finaleLastHand;
        private bool _finaleStatementStarted;

        // 両アクション完了後に GameState.ResolveTurn() へ渡すための、確定済みの抽選結果。
        private RouletteCategory _pendingCategory;
        private Card _pendingDrawnCard;
        private bool _pendingFlipDeckWasReconstituted;

        private void Start()
        {
            _allHandViews = new[] { _bottomHandView, _rightHandView, _topHandView, _leftHandView };

            // 常時表示の手番テキストと開発用のステータス表示は、設定でONにしたときだけ出す（コードは残す）。
            if (_turnIndicatorText != null)
            {
                _turnIndicatorText.gameObject.SetActive(_showTurnIndicatorText);
            }
            if (_statusText != null)
            {
                _statusText.gameObject.SetActive(_showStatusText);
            }

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
                _continueScoreSession = true;
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

            BuildNameplates(startingSeatIndex);
            _discardPile?.Sync(_gameState.DiscardPile);

            // 得点：続けて遊ぶ（もう一度プレイ）なら通算を持ち越し、そうでなければ（または人数が変わったら）リセットする。
            var scoreBook = ScoreSession.Book;
            if (_scoreRules != null)
            {
                scoreBook.Rules = _scoreRules.Rules;
            }
            scoreBook.BeginMatch(playerCount, _gameState.Players.Select(p => p.SeatIndex), _continueScoreSession);
            _continueScoreSession = false;

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

                // 停止した扇形をハイライトしつつ、プレイヤーが結果を確認できるよう少し待ってから戻す。
                StartCoroutine(_rouletteWheelView.HighlightLastStoppedSlice());
            }

            if (_rouletteResultHoldSeconds > 0f)
            {
                yield return new WaitForSeconds(_rouletteResultHoldSeconds);
            }

            // 結果確認の間を置いてから、定位置へ戻す（後続のドロー待ちを止めないよう待機しない）。
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

            // 山札が捨て札から作り直されたときは、残り枚数表示で SHUFFLE! の演出を見せる
            // （下の FocusReshuffle と同じタイミング。演出は待たずにターンを進める）。
            if (reconstituted && _deckCounter != null)
            {
                _deckCounter.PlayReshuffle(_gameState.FlipDeckCount);
            }
            // 同じタイミングで、盤面の捨て札の山が山札の方へ移って空になる。
            if (reconstituted && _discardPile != null)
            {
                _discardPile.PlayReshuffle();
            }
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
            // どの位置のカードが捨てられたかを後で突き合わせるため、判定前の手札を控えておく（読み取りのみ）。
            var handBefore = _gameState.CurrentPlayer.Hand.ToList();
            var result = _gameState.ResolveTurn(_pendingCategory, _pendingDrawnCard, _pendingFlipDeckWasReconstituted);
            bool useNewResult = !_useLegacyResultPopup && _discardPile != null && _bankStamp != null;
            // 運の良い出来事のボーナス（ターンの結果を読み取るだけで判定する）。
            var bonuses = ScoreSession.Book.RecordTurn(result);

            if (!useNewResult)
            {
                // 手札が変化するのは手番プレイヤーの座席のみなので、そこだけ更新すればよい。
                GetHandView(result.TurnPlayer.SeatIndex)?.SetHand(result.TurnPlayer.Hand, faceDown: !result.TurnPlayer.IsHuman);

                // 残り枚数・順位（最後の1人に自動で付く最下位も含む）をネームプレートに反映する。
                RefreshNameplateStates();
            }
            if (result.GameEnded && _nameplateBar != null)
            {
                _nameplateBar.SetCurrentPlayer(-1);
            }
            if (result.GameEnded && _turnSpotlight != null)
            {
                _turnSpotlight.SetSeat(null);
            }

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

            if (useNewResult)
            {
                yield return StartCoroutine(PlayTurnResult(result, handBefore, bonuses));
            }
            else if (_resultPopupView != null)
            {
                yield return StartCoroutine(_resultPopupView.ShowResult(result.DiscardedCards.Count));
            }

            // あがったターンは必ず1枚以上捨てているため、上のトラッシュ枚数ポップアップと
            // あがりの演出が連続して発生する。2つが重ならないよう、トラッシュ表示の
            // 退場が完全に終わってから（上のyieldが完了してから）あがりの演出を始める。
            // あがりの演出（金の紙幣＋札束＋メダル）は終わるまで待ってから、次の手番・結果画面へ進む。
            if (playerFinished)
            {
                yield return StartCoroutine(PlayFinishCelebration(result.FinishedPlayer));
            }

            if (playerFinished && _showFinishMessagePopup && _resultPopupView != null)
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
                // 順位の得点とボーナスを通算に加える（明細はフェーズ2の結果表示で使う）。
                _lastMatchSummary = ScoreSession.Book.FinishMatch(_gameState.Rankings.Select(p => p.SeatIndex).ToList());

                // 結果画面には切り替えず、テーブルの上で最後の演出と取引明細書を見せる。
                if (_finaleView != null && !_useResultScene)
                {
                    StartFinale();
                    yield break;
                }

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
        /// ターンの結果を見せるコルーチン。画面中央にスタンプを押すのと同時に、盤面では
        /// トラッシュなら捨てるカードが金色に光って浮き（CPUのカードは表にめくれ）、基準カードと一緒に捨て札の山へ飛ぶ。
        /// ノートラッシュなら手札がぶるっと震え、基準カードだけが捨て札の山へ飛ぶ。両方が終わるまで待つ。
        /// </summary>
        private IEnumerator PlayTurnResult(TurnResult result, IReadOnlyList<Card> handBefore, IReadOnlyList<BonusAward> bonuses)
        {
            var player = result.TurnPlayer;
            var handView = GetHandView(player.SeatIndex);
            int discardedCount = result.DiscardedCards.Count;

            // カードを引いた後のカメラが定位置へ戻りきってから始める（光る手札が画面の外にならないように）。
            if (_waitForCameraBeforeResult && _cameraDirector != null)
            {
                float waited = 0f;
                while (_cameraDirector.IsMoving && waited < _cameraWaitTimeoutSeconds)
                {
                    waited += Time.deltaTime;
                    yield return null;
                }
            }

            // 山の見た目は、まだ飛んでいない基準カードと捨て札を除いた分に合わせておく（着地ごとに1枚ずつ積む）。
            _discardPile.Sync(_gameState.DiscardPile, excludeTop: 1 + discardedCount);

            var stamp = StartCoroutine(_bankStamp.Play(discardedCount, player.IsHuman, bonuses));
            // ミラクルトラッシュのときは、背景のスロットマシンを一斉に光らせる（あがりのときと同じ大当たりの演出）。
            if (_casinoBackground != null && bonuses != null && bonuses.Any(b => b.Kind == BonusKind.MiracleTrash))
            {
                _casinoBackground.PlayJackpot();
            }

            var indices = DiscardTargets.GetIndices(handBefore, result.DiscardedCards);
            var flyingViews = handView != null ? handView.DetachCardViews(indices) : new List<Card3DView>();
            var flyingCards = indices.Select(i => handBefore[i]).ToList();
            if (flyingViews.Count != flyingCards.Count)
            {
                // 表示と手札がずれていた場合は、飛ばす演出をあきらめて結果だけ反映する。
                foreach (var view in flyingViews)
                {
                    Destroy(view.gameObject);
                }
                flyingViews.Clear();
                flyingCards.Clear();
            }

            Coroutine shake = null;
            if (discardedCount == 0 && handView != null)
            {
                shake = StartCoroutine(_discardPile.Shake(handView.CardViews));
            }

            yield return StartCoroutine(_discardPile.PlayDiscards(flyingViews, flyingCards, revealFaces: !player.IsHuman,
                _referenceCardDrawView, result.DrawnCard, () =>
                {
                    // 全部が飛び立ったら、残った手札を詰めて並べ直し、残り枚数をネームプレートに反映する。
                    handView?.SetHand(player.Hand, faceDown: !player.IsHuman, relayoutSeconds: _handRelayoutSeconds);
                    RefreshNameplateStates();
                }));

            if (shake != null)
            {
                yield return shake;
            }
            yield return stamp;

            _discardPile.Sync(_gameState.DiscardPile);
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

            if (_nameplateBar != null && _gameState != null)
            {
                _nameplateBar.SetCurrentPlayer(_gameState.CurrentPlayer != null ? _gameState.CurrentPlayer.SeatIndex : -1);
            }

            // 手番の紙幣と照らす演出。どちらもターン進行と並行して再生するだけで、ここでは待たない。
            if (_gameState != null && _gameState.CurrentPlayer != null && !_gameState.IsGameOver)
            {
                var current = _gameState.CurrentPlayer;
                if (_turnBanknote != null)
                {
                    int order = _turnOrderBySeat.TryGetValue(current.SeatIndex, out int o) ? o : 1;
                    string message = current.IsHuman ? "あなたの番です" : $"{GetDisplayName(current)}の番です";
                    _turnBanknote.Play(order, message, current.IsHuman);
                }
                if (_turnSpotlight != null)
                {
                    _turnSpotlight.SetSeat(GetSeatSide(current.SeatIndex));
                }
            }

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

        /// <summary>
        /// あがったプレイヤーを、金の紙幣（中央で止まった瞬間に札束がはじけ、ネームプレートにメダルを押す）で
        /// 発表し、紙幣が抜けきって札束も消えるまで待つコルーチン。順位は Player.Rank を読み取るだけ。
        /// </summary>
        // --------------------------------------------------------------
        // ゲーム終了時の最後の演出：① 最下位をいじる → ② 1位を祝う → ③ カメラの上昇と取引明細書。
        // ①②の途中で画面のどこかをタップしたら、途中の演出を片付けて③へ進む。
        // --------------------------------------------------------------

        private void StartFinale()
        {
            var rankings = _gameState.Rankings;
            _finaleLastPlace = rankings[rankings.Count - 1];
            _finaleLastHand = _finaleLastPlace.Hand.ToList();
            _finaleStatementStarted = false;
            _finaleRoutines.Clear();
            _finaleView.ShowSkipCatcher(SkipFinale);
            TrackFinale(FinaleIntro(_finaleLastPlace, rankings[0]));
        }

        private Coroutine TrackFinale(IEnumerator routine)
        {
            var coroutine = StartCoroutine(routine);
            _finaleRoutines.Add(coroutine);
            return coroutine;
        }

        private IEnumerator FinaleIntro(Player lastPlace, Player champion)
        {
            // ① 最下位の人の残りの手札を、いつものトラッシュの動きで1枚ずつ捨て札の山へ放り込む。
            var handView = GetHandView(lastPlace.SeatIndex);
            if (handView != null && _discardPile != null && _finaleLastHand.Count > 0)
            {
                _discardPile.Sync(_gameState.DiscardPile);
                var views = handView.DetachCardViews(Enumerable.Range(0, handView.CardViews.Count).ToList());
                if (views.Count == _finaleLastHand.Count)
                {
                    yield return TrackFinale(_discardPile.PlayDiscards(views, _finaleLastHand, !lastPlace.IsHuman,
                        null, null, null, _finaleThrowStagger));
                }
                else
                {
                    foreach (var view in views)
                    {
                        Destroy(view.gameObject);
                    }
                }
            }

            // 最後に、最下位の人のネームプレートに「TRASH」のスタンプを押す。
            yield return TrackFinale(_finaleView.StampTrash(_nameplateBar != null ? _nameplateBar.GetPlateRect(lastPlace.SeatIndex) : null));
            yield return new WaitForSeconds(_finalePauseAfterTrash);

            // ② 1位を祝う：大きな金の紙幣が中央で止まった瞬間に、スロットが一斉に光り、多めの札束がはじける。メダルも光る。
            if (_nameplateBar != null)
            {
                TrackFinale(_finaleView.GlowMedal(_nameplateBar.GetMedalRect(champion.SeatIndex), _finaleMedalGlowSeconds));
            }
            if (_turnBanknote != null)
            {
                int order = _turnOrderBySeat.TryGetValue(champion.SeatIndex, out int o) ? o : 1;
                string name = champion.IsHuman ? "あなた" : GetDisplayName(champion);
                yield return TrackFinale(_turnBanknote.PlayFinish(order, name, 1, champion.IsHuman, note =>
                {
                    if (_casinoBackground != null)
                    {
                        _casinoBackground.PlayJackpot();
                    }
                    if (_moneyBurst != null)
                    {
                        _moneyBurst.Play(note.position, note.sizeDelta, champion.IsHuman, true, _finaleBurstCountScale);
                    }
                }, _finaleChampionNoteScale, _finaleChampionHoldSeconds, string.Format(_finaleChampionMessage, name)));
            }
            else if (_casinoBackground != null)
            {
                _casinoBackground.PlayJackpot();
            }

            _finaleView.HideSkipCatcher();
            ShowFinaleStatement();
        }

        /// <summary>演出の途中でタップされたとき：途中の演出を残さず片付け、終わった後の状態にしてから③へ進む。</summary>
        private void SkipFinale()
        {
            if (_finaleStatementStarted)
            {
                return;
            }
            foreach (var routine in _finaleRoutines)
            {
                if (routine != null)
                {
                    StopCoroutine(routine);
                }
            }
            _finaleRoutines.Clear();

            if (_discardPile != null)
            {
                _discardPile.CancelFlights();
                _discardPile.Sync(_gameState.DiscardPile.Concat(_finaleLastHand).ToList());
            }
            GetHandView(_finaleLastPlace.SeatIndex)?.SetHand(Array.Empty<Card>(), faceDown: !_finaleLastPlace.IsHuman);
            _turnBanknote?.Cancel();
            _moneyBurst?.Clear();
            _finaleView.ClearTransientEffects();
            if (_nameplateBar != null)
            {
                _finaleView.StampTrashImmediate(_nameplateBar.GetPlateRect(_finaleLastPlace.SeatIndex));
                var medal = _nameplateBar.GetMedalRect(_gameState.Rankings[0].SeatIndex);
                if (medal != null)
                {
                    medal.localScale = Vector3.one;
                }
            }
            ShowFinaleStatement();
        }

        private void ShowFinaleStatement()
        {
            if (_finaleStatementStarted)
            {
                return;
            }
            _finaleStatementStarted = true;
            _finaleView.HideSkipCatcher();
            StartCoroutine(FinaleStatementRoutine());
        }

        private IEnumerator FinaleStatementRoutine()
        {
            // ③ カメラがテーブル全体を見渡す位置まで上昇する（ゲーム終了時のカメラの上昇を流用）。
            if (_cameraDirector != null)
            {
                _cameraDirector.StartCoroutine(_cameraDirector.ZoomOutForGameEnd());
                yield return null;
                float waited = 0f;
                while (_cameraDirector.IsMoving && waited < 3f)
                {
                    waited += Time.deltaTime;
                    yield return null;
                }
            }

            _finaleView.ShowStatement(_lastMatchSummary.MatchNumber, BuildStatementRows(_lastMatchSummary),
                _lastMatchSummary.LeaderChanged, ResultNavigation.PlayAgain, ResultNavigation.ReselectPlayers);
        }

        private static readonly string[] StatementSuits = { "\u2660", "\u2665", "\u2666", "\u2663" };

        private List<StatementRow> BuildStatementRows(MatchSummary summary)
        {
            var rows = new List<StatementRow>(summary.Lines.Count);
            foreach (var line in summary.Lines)
            {
                var player = _gameState.Players.First(p => p.SeatIndex == line.SeatIndex);
                int order = _turnOrderBySeat.TryGetValue(line.SeatIndex, out int o) ? o : 1;
                int suitIndex = (Mathf.Max(1, order) - 1) % StatementSuits.Length;
                bool red = suitIndex == 1 || suitIndex == 2;
                rows.Add(new StatementRow
                {
                    Rank = line.Rank,
                    MedalColor = _nameplateBar != null ? _nameplateBar.MedalColor(line.Rank) : Color.white,
                    Suit = StatementSuits[suitIndex],
                    SuitColor = _nameplateBar != null ? (red ? _nameplateBar.SuitRedColor : _nameplateBar.SuitBlackColor) : Color.black,
                    Name = GetDisplayName(player),
                    IsHuman = player.IsHuman,
                    RankPoints = line.RankPoints,
                    Bonuses = line.Bonuses,
                    Deposit = line.Deposit,
                    PreviousBalance = line.PreviousBalance,
                    Balance = line.Balance,
                    IsLeader = summary.LeaderSeats.Contains(line.SeatIndex)
                });
            }
            return rows;
        }

        private IEnumerator PlayFinishCelebration(Player finished)
        {
            int rank = finished.Rank ?? 0;
            int order = _turnOrderBySeat.TryGetValue(finished.SeatIndex, out int o) ? o : 1;
            string name = finished.IsHuman ? "あなた" : GetDisplayName(finished);

            if (_turnBanknote == null)
            {
                _nameplateBar?.StampFinishMedal(finished.SeatIndex, rank);
                yield break;
            }

            yield return StartCoroutine(_turnBanknote.PlayFinish(order, name, rank, finished.IsHuman, note =>
            {
                if (_moneyBurst != null)
                {
                    _moneyBurst.Play(note.position, note.sizeDelta, finished.IsHuman, rank == 1);
                }
                if (_casinoBackground != null)
                {
                    _casinoBackground.PlayJackpot();
                }
                if (_nameplateBar != null && rank > 0)
                {
                    _nameplateBar.StampFinishMedal(finished.SeatIndex, rank);
                }
            }));

            // 札束がまだ舞っていれば、消えきるまで待つ（次の手番の紙幣と同時に出さないため）。
            while (_moneyBurst != null && _moneyBurst.RemainingSeconds > 0f)
            {
                yield return null;
            }
        }

        /// <summary>
        /// 画面左上のネームプレートを、開始プレイヤーから時計回りの手番の順（1P, 2P, ...）で作る。
        /// 手番の順は GameState と同じ規則（SeatIndex の昇順を開始座席から1周）で、読み取りだけで決める。
        /// </summary>
        private void BuildNameplates(int startingSeatIndex)
        {
            if (_nameplateBar == null || _gameState == null)
            {
                return;
            }

            var order = NameplateOrder.GetTurnOrder(_gameState.Players.Select(p => p.SeatIndex), startingSeatIndex);
            var infos = new List<NameplateInfo>(order.Count);
            _turnOrderBySeat.Clear();
            for (int i = 0; i < order.Count; i++)
            {
                var player = _gameState.Players.First(p => p.SeatIndex == order[i]);
                _turnOrderBySeat[player.SeatIndex] = i + 1;
                infos.Add(new NameplateInfo(i + 1, player.SeatIndex, GetDisplayName(player),
                    MiniTableSeats.ToScreenPosition(GetSeatSide(player.SeatIndex)), player.IsHuman));
            }

            _nameplateBar.Build(infos);
            RefreshNameplateStates();
        }

        private void RefreshNameplateStates()
        {
            if (_nameplateBar == null || _gameState == null)
            {
                return;
            }

            foreach (var player in _gameState.Players)
            {
                _nameplateBar.SetPlayerState(player.SeatIndex, player.Hand.Count, player.Rank);
            }
        }

        private void UpdateDeckCountLabel()
        {
            if (_deckCountText != null && _gameState != null)
            {
                _deckCountText.text = $"残り{_gameState.FlipDeckCount}枚";
            }

            if (_deckCounter != null && _gameState != null)
            {
                _deckCounter.SetCount(_gameState.FlipDeckCount);
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
