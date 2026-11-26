using System.Runtime.InteropServices;
using UnityEngine;

namespace Client.Game.InGame.Block.GearRotation.Gpu
{
    /// <summary>
    /// GPUの表の1行。レイアウトはGearRotation.hlslのGearRotationRowと一致させる
    /// One row of the GPU table; the layout must match GearRotationRow in GearRotation.hlsl
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct GearRotationGpuRow
    {
        public const int Stride = sizeof(int) * 4 + sizeof(float) * 4;
        public const int EmptyState = 0;
        public const int UsedState = 1;
        public const int RemovedState = 2;

        public Vector3Int Key;
        public int SlotState;
        // 基準時刻時点の符号付き角度・絶対角度(deg)、符号付き角速度(deg/s)、基準時刻(s)
        // Signed and absolute angle at the base time (deg), signed angular speed (deg/s), base time (s)
        public float SignedAngle;
        public float AbsoluteAngle;
        public float SignedDegreesPerSecond;
        public float BaseTime;
    }
}
