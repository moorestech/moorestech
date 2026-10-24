using UnityEngine;

namespace Game.CleanRoom.Info.Leak
{
    /// <summary>
    ///     漏れ探索で塗りを閉じ込める軸平行の箱。面ごとに1層ずつ広げられる
    ///     Axis-aligned box confining the leak-probe fill; each face can grow one layer at a time
    /// </summary>
    public class CleanRoomLeakBox
    {
        public Vector3Int Min { get; private set; }
        public Vector3Int Max { get; private set; }

        public CleanRoomLeakBox(Vector3Int cell)
        {
            Min = cell;
            Max = cell;
        }

        public void Include(Vector3Int cell)
        {
            Min = Vector3Int.Min(Min, cell);
            Max = Vector3Int.Max(Max, cell);
        }

        public bool Contains(Vector3Int cell)
        {
            return Min.x <= cell.x && cell.x <= Max.x && Min.y <= cell.y && cell.y <= Max.y && Min.z <= cell.z && cell.z <= Max.z;
        }

        // direction 側の面上にあるセルか
        // Whether the cell lies on the face toward direction
        public bool IsOnFace(Vector3Int cell, Vector3Int direction)
        {
            var axis = CleanRoomLeakRays.AxisOf(direction);
            var face = 0 < direction[axis] ? Max[axis] : Min[axis];
            return cell[axis] == face;
        }

        // direction 以外の2軸で箱の最小・最大に並ぶ（側面の縁にある）か
        // Whether the cell sits on the min or max of either axis other than direction's (the side rim)
        public bool IsOnLateralEdge(Vector3Int cell, Vector3Int direction)
        {
            var normalAxis = CleanRoomLeakRays.AxisOf(direction);
            for (var axis = 0; axis < 3; axis++)
            {
                if (axis == normalAxis) continue;
                if (cell[axis] == Min[axis] || cell[axis] == Max[axis]) return true;
            }

            return false;
        }

        public void GrowFace(Vector3Int direction)
        {
            if (0 < direction[CleanRoomLeakRays.AxisOf(direction)]) Max += direction;
            else Min += direction;
        }
    }
}
