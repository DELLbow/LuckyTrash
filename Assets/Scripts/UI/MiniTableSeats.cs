using System.Collections.Generic;

namespace LuckyTrash.UI
{
    /// <summary>
    /// ネームプレートのミニテーブル図で、どの座席の丸を使う（塗る）かを決める純粋関数。
    /// 座席（<see cref="CameraDirector.SeatSide"/>）を、通常のカメラから見た画面上の位置に変換する。
    /// 通常のカメラはテーブルの手前（Z+側）から -Z 方向を向いている（ワールドではY軸まわりに180度回転している）ため、
    /// ワールドの右座席（RightSeatAnchor3D、X+側）は画面の左に、左座席は画面の右に見える。
    /// ワールド座標の左右ではなく、この画面上の位置で図を描く。
    /// </summary>
    public static class MiniTableSeats
    {
        /// <summary>座席を、通常のカメラから見た画面上の位置に変換する。</summary>
        public static NameplateSeatDirection ToScreenPosition(CameraDirector.SeatSide side)
        {
            switch (side)
            {
                case CameraDirector.SeatSide.Right: return NameplateSeatDirection.Left;
                case CameraDirector.SeatSide.Left: return NameplateSeatDirection.Right;
                case CameraDirector.SeatSide.Top: return NameplateSeatDirection.Up;
                default: return NameplateSeatDirection.Down;
            }
        }

        /// <summary>参加している座席の一覧から、図に丸を表示する画面上の位置の集合を返す（使わない席は含まない）。</summary>
        public static HashSet<NameplateSeatDirection> GetOccupiedPositions(IEnumerable<CameraDirector.SeatSide> occupiedSides)
        {
            var positions = new HashSet<NameplateSeatDirection>();
            foreach (var side in occupiedSides)
            {
                positions.Add(ToScreenPosition(side));
            }
            return positions;
        }
    }
}
