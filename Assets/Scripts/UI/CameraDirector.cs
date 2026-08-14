using System.Collections;
using UnityEngine;

namespace LuckyTrash.UI
{
    /// <summary>
    /// GameScene の Main Camera にアタッチし、ターン進行に合わせたカメラ演出（ルーレット注視、
    /// ドロー注視、ターン開始パン、あがりズームイン、ゲーム終了ズームアウト、再シャッフル注視）を
    /// 一元管理するコンポーネント。
    ///
    /// 設計方針:
    /// - 起動時（<see cref="Awake"/>）の position / rotation / FOV を「定位置」として記録し、
    ///   各演出はそこからの遷移として表現する。
    /// - 移動は position / rotation / FOV を同時に補間する単一のコルーチン（<see cref="RunMove"/>）で
    ///   行い、<see cref="_easeCurve"/> でイージングをかける。
    /// - 新しい移動要求が来たときは、進行中の移動コルーチンを止めて「現在の姿勢」から新しい目標へ
    ///   改めて補間を開始する（<see cref="BeginMove"/>）。目標を記憶して差分を取るのではなく、
    ///   Transform/Camera の現在値をそのまま補間の開始点にするため、演出が重なってもカクつかない。
    /// - <see cref="_waitForCameraArrival"/> が false（既定）の間は、「Focus〜」系の呼び出しは
    ///   移動を裏で開始してすぐに戻る（＝呼び出し元の演出と並行して動く）。true にすると、
    ///   移動が完了するまで呼び出し元をブロックする（＝カメラ到着後に演出開始、に切り替わる）。
    ///   「Return〜」系（定位置へ戻す）は、後続の演出を待たせる必要が無いため常に裏で動く。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraDirector : MonoBehaviour
    {
        /// <summary>4座席の方向。ターン進行側は SeatIndex ではなくこちらでカメラに指示する。</summary>
        public enum SeatSide
        {
            Bottom,
            Right,
            Top,
            Left
        }

        [Header("基本設定")]
        [Tooltip("ONにすると、Focus系の演出はカメラが目標地点へ到着してから呼び出し元へ処理を返す" +
            "（＝演出開始が「カメラ到着後」になる）。OFF（既定）では、移動を裏で開始してすぐ処理を返す" +
            "（＝ボタン押下と同時にカメラが動き出し、既存演出と並行して動く）。")]
        [SerializeField] private bool _waitForCameraArrival = false;

        [Tooltip("移動の緩急づけ（0=開始、1=到着）。既定はease-in-out。")]
        [SerializeField] private AnimationCurve _easeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("参照: テーブル上のアンカー")]
        [SerializeField] private Transform _bottomSeatAnchor;
        [SerializeField] private Transform _rightSeatAnchor;
        [SerializeField] private Transform _topSeatAnchor;
        [SerializeField] private Transform _leftSeatAnchor;
        [SerializeField] private Transform _deckAnchor;
        [SerializeField] private Transform _rouletteAnchor;
        [SerializeField] private Transform _refCardAnchor;

        [Header("演出1: ルーレット時のトップダウン")]
        [SerializeField] private float _rouletteFocusHeight = 1.5f;
        [SerializeField] private float _rouletteFocusForwardOffset = 1.0f;
        [SerializeField] private float _rouletteFocusFov = 46f;
        [SerializeField] private float _rouletteFocusDurationSeconds = 0.55f;
        [SerializeField] private float _rouletteReturnDurationSeconds = 0.5f;

        [Header("演出2: ドロー時の山札フォーカス")]
        [SerializeField] private float _drawFocusHeight = 1.3f;
        [SerializeField] private float _drawFocusForwardOffset = 1.4f;
        [SerializeField] private float _drawFocusFov = 50f;
        [SerializeField] private float _drawFocusDurationSeconds = 0.5f;
        [SerializeField] private float _drawReturnDurationSeconds = 0.45f;

        [Header("演出3: ターン開始時のパン（控えめ）")]
        [Tooltip("定位置から座席方向へ寄る距離（ワールド単位）。控えめに。")]
        [SerializeField] private float _turnPanDistance = 0.3f;
        [Tooltip("座席方向を見る度合い（0=向きを変えない、1=完全に座席を注視）。小さめの値を推奨。")]
        [SerializeField] private float _turnPanLookBlend = 0.18f;
        [SerializeField] private float _turnPanFovDelta = -2f;
        [SerializeField] private float _turnPanDurationSeconds = 0.6f;

        [Header("演出4: あがり演出（ズームイン）")]
        [SerializeField] private float _finishZoomHeight = 0.9f;
        [SerializeField] private float _finishZoomForwardOffset = 0.6f;
        [SerializeField] private float _finishZoomFov = 38f;
        [SerializeField] private float _finishZoomDurationSeconds = 0.9f;

        [Header("演出5: ゲーム終了時のズームアウト")]
        [SerializeField] private float _gameEndZoomOutHeight = 3.2f;
        [SerializeField] private float _gameEndZoomOutForwardOffset = 2.2f;
        [SerializeField] private float _gameEndZoomOutFov = 72f;
        [SerializeField] private float _gameEndZoomOutDurationSeconds = 1.6f;
        [Tooltip("ズームアウト後、シーン遷移前に静止して見せる秒数。")]
        [SerializeField] private float _gameEndZoomOutSeconds = 2f;

        [Header("演出6: 再シャッフル時のフォーカス")]
        [SerializeField] private float _reshuffleFocusHeight = 1.0f;
        [SerializeField] private float _reshuffleFocusForwardOffset = 0.8f;
        [SerializeField] private float _reshuffleFocusFov = 42f;
        [SerializeField] private float _reshuffleFocusDurationSeconds = 0.5f;

        private Camera _camera;
        private Vector3 _defaultPosition;
        private Quaternion _defaultRotation;
        private float _defaultFov;
        private Coroutine _activeMove;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _defaultPosition = transform.position;
            _defaultRotation = transform.rotation;
            _defaultFov = _camera != null ? _camera.fieldOfView : 60f;
        }

        // --------------------------------------------------------------
        // 演出1: ルーレット
        // --------------------------------------------------------------

        public IEnumerator FocusRoulette()
        {
            if (_rouletteAnchor == null)
            {
                yield break;
            }

            yield return FocusPoint(_rouletteAnchor.position, _rouletteFocusHeight, _rouletteFocusForwardOffset,
                _rouletteFocusFov, _rouletteFocusDurationSeconds);
        }

        public void ReturnFromRoulette()
        {
            ReturnToDefault(_rouletteReturnDurationSeconds);
        }

        // --------------------------------------------------------------
        // 演出2: ドロー（演出6の再シャッフルフォーカスもここから連続して呼ばれる想定）
        // --------------------------------------------------------------

        public IEnumerator FocusDraw()
        {
            if (_deckAnchor == null || _refCardAnchor == null)
            {
                yield break;
            }

            Vector3 midPoint = (_deckAnchor.position + _refCardAnchor.position) * 0.5f;
            yield return FocusPoint(midPoint, _drawFocusHeight, _drawFocusForwardOffset,
                _drawFocusFov, _drawFocusDurationSeconds);
        }

        public void ReturnFromDraw()
        {
            ReturnToDefault(_drawReturnDurationSeconds);
        }

        // --------------------------------------------------------------
        // 演出3: ターン開始パン
        // --------------------------------------------------------------

        /// <summary>
        /// 定位置から、指定座席の方向へ軽くシフト・傾ける。移動要求は他の演出と同様に中断・接続されるため、
        /// 呼び出し元（GameController.StartNextTurn、非コルーチン）は待たずに呼び出してよい。
        /// </summary>
        public void PanToTurn(SeatSide side)
        {
            Transform anchor = GetSeatAnchor(side);
            if (anchor == null)
            {
                return;
            }

            Vector3 toSeat = anchor.position - _defaultPosition;
            Vector3 dir = toSeat.sqrMagnitude > 0.0001f ? toSeat.normalized : Vector3.forward;
            Vector3 targetPos = _defaultPosition + dir * _turnPanDistance;

            Quaternion lookAtSeat = Quaternion.LookRotation((anchor.position - targetPos).normalized, Vector3.up);
            Quaternion targetRot = Quaternion.Slerp(_defaultRotation, lookAtSeat, _turnPanLookBlend);

            float targetFov = _defaultFov + _turnPanFovDelta;

            BeginMove(targetPos, targetRot, targetFov, _turnPanDurationSeconds);
        }

        // --------------------------------------------------------------
        // 演出4: あがり演出
        // --------------------------------------------------------------

        public IEnumerator FocusFinish(SeatSide side)
        {
            Transform anchor = GetSeatAnchor(side);
            if (anchor == null)
            {
                yield break;
            }

            yield return FocusPoint(anchor.position, _finishZoomHeight, _finishZoomForwardOffset,
                _finishZoomFov, _finishZoomDurationSeconds);
        }

        // あがり演出からの復帰は明示的な Return を持たない。次のターン（StartNextTurn）の
        // PanToTurn が、現在カメラがどこにあっても新しい目標へ滑らかに繋いでくれるため、
        // ゲームが続く場合は自動的に「定位置または次の手番のパン位置」へ戻る。
        // ゲームが終了する場合は ZoomOutForGameEnd がそのまま引き継ぐ。

        // --------------------------------------------------------------
        // 演出5: ゲーム終了ズームアウト
        // --------------------------------------------------------------

        /// <summary>
        /// テーブル全体を見せるズームアウトを行い、<see cref="_gameEndZoomOutSeconds"/> 秒静止する。
        /// waitForCameraArrival の設定に関わらず、シーン遷移前に必ず最後まで見せる（呼び出し元は
        /// このコルーチンの完了を待ってから SceneManager.LoadScene を呼ぶ想定）。
        /// </summary>
        public IEnumerator ZoomOutForGameEnd()
        {
            Vector3 horizontalDir = new Vector3(_defaultPosition.x, 0f, _defaultPosition.z);
            Vector3 dir = horizontalDir.sqrMagnitude > 0.0001f ? horizontalDir.normalized : Vector3.forward;
            Vector3 targetPos = _defaultPosition + Vector3.up * _gameEndZoomOutHeight + dir * _gameEndZoomOutForwardOffset;

            var handle = BeginMove(targetPos, _defaultRotation, _gameEndZoomOutFov, _gameEndZoomOutDurationSeconds);
            yield return handle;
            yield return new WaitForSeconds(_gameEndZoomOutSeconds);
        }

        // --------------------------------------------------------------
        // 演出6: 再シャッフル時のフォーカス
        // --------------------------------------------------------------

        public IEnumerator FocusReshuffle()
        {
            if (_deckAnchor == null)
            {
                yield break;
            }

            yield return FocusPoint(_deckAnchor.position, _reshuffleFocusHeight, _reshuffleFocusForwardOffset,
                _reshuffleFocusFov, _reshuffleFocusDurationSeconds);
        }

        // --------------------------------------------------------------
        // 共通ヘルパー
        // --------------------------------------------------------------

        private Transform GetSeatAnchor(SeatSide side)
        {
            switch (side)
            {
                case SeatSide.Right:
                    return _rightSeatAnchor;
                case SeatSide.Top:
                    return _topSeatAnchor;
                case SeatSide.Left:
                    return _leftSeatAnchor;
                case SeatSide.Bottom:
                default:
                    return _bottomSeatAnchor;
            }
        }

        /// <summary>
        /// worldPoint を、定位置のカメラがいた側（水平方向）からheight/forwardOffset分だけ
        /// 離れた位置から見下ろす形で注視する。座席・ルーレット・山札など、注視点を指定するだけで
        /// 「元のカメラ側から見た自然な寄り」になるようにするための共通ロジック。
        /// </summary>
        private IEnumerator FocusPoint(Vector3 worldPoint, float height, float forwardOffset, float fov, float duration)
        {
            Vector3 approachDir = ApproachDirection(worldPoint);
            Vector3 targetPos = worldPoint + Vector3.up * height + approachDir * forwardOffset;
            Quaternion targetRot = Quaternion.LookRotation((worldPoint - targetPos).normalized, Vector3.up);

            var handle = BeginMove(targetPos, targetRot, fov, duration);
            if (_waitForCameraArrival)
            {
                yield return handle;
            }
        }

        private Vector3 ApproachDirection(Vector3 worldPoint)
        {
            Vector3 flat = _defaultPosition - worldPoint;
            flat.y = 0f;
            return flat.sqrMagnitude > 0.0001f ? flat.normalized : Vector3.forward;
        }

        private void ReturnToDefault(float duration)
        {
            BeginMove(_defaultPosition, _defaultRotation, _defaultFov, duration);
        }

        /// <summary>
        /// 進行中の移動があれば中断し、現在の姿勢から新しい目標への移動を新たに開始する。
        /// 「目標を記憶して差分を取る」のではなく毎回現在値を起点にするため、演出が重なっても
        /// カクついたり飛んだりしない。
        /// </summary>
        private Coroutine BeginMove(Vector3 targetPos, Quaternion targetRot, float targetFov, float duration)
        {
            if (_activeMove != null)
            {
                StopCoroutine(_activeMove);
            }

            _activeMove = StartCoroutine(RunMove(targetPos, targetRot, targetFov, duration));
            return _activeMove;
        }

        private IEnumerator RunMove(Vector3 targetPos, Quaternion targetRot, float targetFov, float duration)
        {
            Vector3 startPos = transform.position;
            Quaternion startRot = transform.rotation;
            float startFov = _camera != null ? _camera.fieldOfView : targetFov;

            if (duration > 0f)
            {
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    float eased = _easeCurve.Evaluate(t);

                    transform.position = Vector3.LerpUnclamped(startPos, targetPos, eased);
                    transform.rotation = Quaternion.SlerpUnclamped(startRot, targetRot, eased);
                    if (_camera != null)
                    {
                        _camera.fieldOfView = Mathf.LerpUnclamped(startFov, targetFov, eased);
                    }

                    yield return null;
                }
            }

            transform.position = targetPos;
            transform.rotation = targetRot;
            if (_camera != null)
            {
                _camera.fieldOfView = targetFov;
            }

            _activeMove = null;
        }
    }
}
