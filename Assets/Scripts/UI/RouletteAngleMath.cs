using UnityEngine;

namespace LuckyTrash.UI
{
    /// <summary>
    /// ルーレットの円盤(<see cref="RouletteWheelView"/>の<c>_disc</c>)の回転角度計算を、
    /// MonoBehaviourから切り離した純粋関数として提供するクラス。
    /// 扇形は6分割（60度ずつ）、インデックス0の中心角は30度、以降60度刻み
    /// （<see cref="LuckyTrash.Game.RouletteSelector.AllFaces"/>のインデックスと対応）。
    /// ポインターは常にワールド角度0度（<c>_disc</c>の親であるルーレットroot基準、回転前のローカル+Z方向）に固定されている。
    /// </summary>
    public static class RouletteAngleMath
    {
        /// <summary>扇形1枚あたりの中心角（度）。6分割なので60度。</summary>
        public const float SliceAngle = 360f / 6f;

        /// <summary>
        /// 指定した扇形インデックスの扇形が、停止後にポインター位置（ワールド角度0度）へ来るために必要な、
        /// _discのローカルY回転角（0〜360度未満に正規化）を返す。
        /// </summary>
        /// <remarks>
        /// _discをワールドのY軸正方向（Unityの左手系でQuaternion.Euler(0, R, 0)を適用した向き）に角度Rだけ回転させると、
        /// _disc上のローカル角度θの点はワールド角度(θ + R)へ移動する。
        /// 扇形iの中心角はθ_i = i*SliceAngle + SliceAngle/2 なので、
        /// θ_i + R ≡ 0 (mod 360) を満たすR、すなわちR = -θ_i (mod 360) が求める回転角になる。
        /// </remarks>
        public static float GetRestAngleForSlice(int sliceIndex)
        {
            float sliceMidAngle = sliceIndex * SliceAngle + SliceAngle / 2f;
            return Mathf.Repeat(-sliceMidAngle, 360f);
        }

        /// <summary>
        /// 開始角度startAngleから、restAngle（0〜360度未満に正規化済み）へ向けて、
        /// 少なくとも0度以上かつextraTurns回分の全周を加えた、最終的な累積回転角（360度を超えうる）を返す。
        /// 常にstartAngle以上の値になる（逆回転させない＝見た目の回転方向を変えない）。
        /// </summary>
        public static float ComputeTargetAngle(float startAngle, float restAngle, int extraTurns)
        {
            float normalizedDelta = Mathf.Repeat(restAngle - startAngle, 360f);
            return startAngle + 360f * extraTurns + normalizedDelta;
        }

        /// <summary>
        /// _discの最終的なローカルY回転角（任意の値、360度超・負値可）から、
        /// その時点でポインター位置（ワールド角度0度）に来ている扇形のインデックス（0〜5）を求める。
        /// テスト・検証用（<see cref="GetRestAngleForSlice"/>の逆関数）。
        /// </summary>
        public static int GetSliceIndexAtPointer(float discFinalAngleY)
        {
            float value = Mathf.Repeat(-SliceAngle / 2f - discFinalAngleY, 360f) / SliceAngle;
            int index = Mathf.RoundToInt(value) % 6;
            return index;
        }
    }
}
