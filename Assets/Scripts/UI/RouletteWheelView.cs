using System;
using System.Collections;
using System.Collections.Generic;
using LuckyTrash.Game;
using UnityEngine;

namespace LuckyTrash.UI
{
    /// <summary>
    /// ルーレットの回転演出を担当するビュー。
    /// RouletteSelector 自体の抽選ロジックには一切関与せず、既に決まった
    /// <see cref="RouletteCategory"/> の結果を受け取り、見た目上どの扇形（同じカテゴリの
    /// 扇形が複数ある場合はその中からランダムに1つ）にポインターが合うように円盤(<see cref="_disc"/>、
    /// テーブル上に平らに置かれた3Dオブジェクト)をワールドY軸まわりに回転させるだけの純粋な演出
    /// コンポーネント。円盤の扇形構成は <see cref="RouletteSelector.AllFaces"/>（Number3/Suit2/Color1、
    /// 60度ずつ6分割）とインデックスを完全に一致させている。
    /// </summary>
    public class RouletteWheelView : MonoBehaviour
    {
        private const float SliceAngle = RouletteAngleMath.SliceAngle;

        [SerializeField] private Transform _disc;
        [SerializeField] private float _spinDurationSeconds = 1.2f;
        [SerializeField] private int _minExtraTurns = 3;
        [SerializeField] private int _maxExtraTurns = 5; // Random.Range上限は排他的なので実際は3,4,5が出る
        [SerializeField] private AnimationCurve _easingCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("停止後のハイライト演出")]
        [Tooltip("停止した扇形を明るく光らせてから元に戻すまでの合計時間（秒）。")]
        [SerializeField] private float _highlightFlashDurationSeconds = 0.4f;
        [Tooltip("ハイライトのピーク時の半透明オーバーレイのアルファ値（0=見えない、1=不透明）。")]
        [Range(0f, 1f)] [SerializeField] private float _highlightPeakAlpha = 0.6f;

        private static readonly IReadOnlyList<RouletteCategory> SliceCategories = RouletteSelector.AllFaces;

        /// <summary>演出（回転アニメーション）が進行中かどうか。</summary>
        public bool IsSpinning { get; private set; }

        /// <summary>直近のSpinToで停止し、ポインター位置に来た扇形のインデックス（0-5）。</summary>
        public int LastStoppedSliceIndex { get; private set; } = -1;

        /// <summary>
        /// 指定されたカテゴリの扇形（同カテゴリが複数あればランダムに1つ）にポインターが合うまで、
        /// 円盤を数回転させながら減速して停止するコルーチン。
        /// </summary>
        public IEnumerator SpinTo(RouletteCategory resultCategory)
        {
            if (_disc == null)
            {
                yield break;
            }

            IsSpinning = true;

            int chosenSliceIndex = ChooseSliceIndex(resultCategory);
            float restAngle = RouletteAngleMath.GetRestAngleForSlice(chosenSliceIndex);

            float startAngle = _disc.localEulerAngles.y;
            int extraTurns = UnityEngine.Random.Range(_minExtraTurns, _maxExtraTurns + 1);

            float targetAngle = RouletteAngleMath.ComputeTargetAngle(startAngle, restAngle, extraTurns);

            float elapsed = 0f;
            while (elapsed < _spinDurationSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _spinDurationSeconds);
                float eased = _easingCurve.Evaluate(t);
                float currentY = Mathf.LerpUnclamped(startAngle, targetAngle, eased);
                _disc.localRotation = Quaternion.Euler(0f, currentY, 0f);
                yield return null;
            }

            _disc.localRotation = Quaternion.Euler(0f, targetAngle, 0f);
            LastStoppedSliceIndex = chosenSliceIndex;
            IsSpinning = false;
        }

        /// <summary>
        /// 直近に<see cref="SpinTo"/>で止まった扇形の上に、同じ形の半透明オーバーレイ
        /// (Disc/GeneratedDecorations/HighlightOverlay、<see cref="RouletteWheelDecorator"/>が生成)を
        /// 重ねて一度光らせてから消す。扇形は1枚の焼き込みテクスチャのため、マスごとに色を変える方式は
        /// 使えない。オーバーレイの角度はRouletteAngleMathと同じ式（bisector = index*60+30）で求めるため、
        /// 抽選・回転の仕組みには一切影響しない。
        /// </summary>
        public IEnumerator HighlightLastStoppedSlice()
        {
            if (_disc == null || LastStoppedSliceIndex < 0)
            {
                yield break;
            }

            var overlay = _disc.Find("GeneratedDecorations/HighlightOverlay");
            if (overlay == null)
            {
                yield break;
            }

            var renderer = overlay.GetComponent<Renderer>();
            if (renderer == null)
            {
                yield break;
            }

            float phi = LastStoppedSliceIndex * SliceAngle + SliceAngle / 2f;
            overlay.localRotation = Quaternion.Euler(0f, phi, 0f);

            var mat = renderer.material; // このオーバーレイ専用のインスタンスなので直接書き換えてよい。
            Color baseColor = mat.color;
            float half = Mathf.Max(0.01f, _highlightFlashDurationSeconds / 2f);

            float t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                float a = Mathf.Lerp(0f, _highlightPeakAlpha, Mathf.Clamp01(t / half));
                mat.color = new Color(baseColor.r, baseColor.g, baseColor.b, a);
                yield return null;
            }

            t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                float a = Mathf.Lerp(_highlightPeakAlpha, 0f, Mathf.Clamp01(t / half));
                mat.color = new Color(baseColor.r, baseColor.g, baseColor.b, a);
                yield return null;
            }

            mat.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
        }

        private static int ChooseSliceIndex(RouletteCategory resultCategory)
        {
            var candidates = new List<int>();
            for (int i = 0; i < SliceCategories.Count; i++)
            {
                if (SliceCategories[i] == resultCategory)
                {
                    candidates.Add(i);
                }
            }

            if (candidates.Count == 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(resultCategory), resultCategory, "No wheel slice matches the given category.");
            }

            return candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }
    }
}
