using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor
{
    /// <summary>
    /// 歯車回転のワールド符号規約を計算する純ロジック。
    /// 規約: IsClockwise=true は「回転軸のワールド正方向(+X/+Y/+Z)から見てUnityの正回転」を意味する。
    /// これにより設置方向(Yaw)が180度違っても同一ネットワークの見た目回転方向が一致する。
    ///
    /// Pure logic for the world-sign convention of gear rotation.
    /// Convention: IsClockwise=true means positive Unity rotation viewed from the positive world axis (+X/+Y/+Z).
    /// This keeps the apparent spin direction consistent across placement directions differing by 180 degrees.
    /// </summary>
    public static class GearWorldRotationSign
    {
        public static Vector3 ToAxisVector(RotationAxis axis)
        {
            return axis switch
            {
                RotationAxis.X => Vector3.right,
                RotationAxis.Y => Vector3.up,
                RotationAxis.Z => Vector3.forward,
                _ => Vector3.zero,
            };
        }

        public static float GetWorldAxisSign(Quaternion worldRotation, RotationAxis axis)
        {
            var worldAxis = worldRotation * ToAxisVector(axis);

            // 支配成分(絶対値最大)の符号を採用。ブロックは軸整列配置なので厳密に決まる
            // Use the sign of the dominant (largest absolute) component; block placement is axis-aligned so this is exact
            var absX = Mathf.Abs(worldAxis.x);
            var absY = Mathf.Abs(worldAxis.y);
            var absZ = Mathf.Abs(worldAxis.z);

            if (absX >= absY && absX >= absZ) return worldAxis.x >= 0 ? 1f : -1f;
            if (absY >= absZ) return worldAxis.y >= 0 ? 1f : -1f;
            return worldAxis.z >= 0 ? 1f : -1f;
        }
    }
}
