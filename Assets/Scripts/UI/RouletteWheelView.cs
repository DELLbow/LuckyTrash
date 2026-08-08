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
    /// 扇形が複数ある場合はその中からランダムに1つ）にポインターが合うように円盤(Disc)を
    /// 回転させるだけの純粋な演出コンポーネント。
    /// 円盤の扇形構成は <see cref="RouletteSelector.AllFaces"/>（Number3/Suit2/Color1、
    /// 60度ずつ6分割）とインデックスを完全に一致させている。
    /// </summary>
    public class RouletteWheelView : MonoBehaviour
    {
        private const float SliceAngle = 360f / 6f;

        [SerializeField] private RectTransform _disc;
        [SerializeField] private float _spinDurationSeconds = 1.2f;
        [SerializeField] private int _minExtraTurns = 3;
        [SerializeField] private int _maxExtraTurns = 5; // Random.Range上限は排他的なので実際は3,4,5が出る
        [SerializeField] private AnimationCurve _easingCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private static readonly IReadOnlyList<RouletteCategory> SliceCategories = RouletteSelector.AllFaces;

        /// <summary>演出（回転アニメーション）が進行中かどうか。</summary>
        public bool IsSpinning { get; private set; }

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
            float sliceMidAngle = chosenSliceIndex * SliceAngle + SliceAngle / 2f;

            float startAngle = _disc.localEulerAngles.z;
            int extraTurns = UnityEngine.Random.Range(_minExtraTurns, _maxExtraTurns + 1);

            // startAngle からポインター位置(sliceMidAngle)まで、少なくとも0度は進むように正規化した差分。
            float normalizedDelta = Mathf.Repeat(sliceMidAngle - startAngle, 360f);
            float targetAngle = startAngle + 360f * extraTurns + normalizedDelta;

            float elapsed = 0f;
            while (elapsed < _spinDurationSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _spinDurationSeconds);
                float eased = _easingCurve.Evaluate(t);
                float currentZ = Mathf.LerpUnclamped(startAngle, targetAngle, eased);
                _disc.localRotation = Quaternion.Euler(0f, 0f, currentZ);
                yield return null;
            }

            _disc.localRotation = Quaternion.Euler(0f, 0f, targetAngle);
            IsSpinning = false;
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
