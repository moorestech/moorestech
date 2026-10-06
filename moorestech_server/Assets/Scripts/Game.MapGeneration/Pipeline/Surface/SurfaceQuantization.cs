using System;
using Game.MapGeneration.Facade.Surface;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Surface
{
    public static class SurfaceQuantization
    {
        public static float LandFloor(float terrainHeight, SurfaceEnvelope envelope)
        {
            double minimum = (double)envelope.SeaY + envelope.MaximumWaveRise + envelope.LandClearance;
            int units = (int)Math.Ceiling(minimum / terrainHeight * ushort.MaxValue);

            // 読み戻したfloat高さで下限を満たす段まで進める
            // Advance until the decoded float height satisfies the lower bound
            while (units <= ushort.MaxValue && Decode(units, terrainHeight) < minimum) units++;
            ValidateUnits(units, terrainHeight);
            return Decode(units, terrainHeight);
        }

        public static float PadHeight(int boxBottom, float terrainHeight)
        {
            double maximum = boxBottom - 0.001d;
            int units = (int)Math.Floor(maximum / terrainHeight * ushort.MaxValue);

            // 書出しと読戻しを通る値で採掘底面を侵さない
            // Keep the serialized and decoded height below the mining bottom
            while (units >= 0 && Decode(units, terrainHeight) > maximum) units--;
            ValidateUnits(units, terrainHeight);
            return Decode(units, terrainHeight);
        }

        public static float Quantize(float heightMeters, float terrainHeight)
        {
            int units = Mathf.RoundToInt(Mathf.Clamp01(heightMeters / terrainHeight) * ushort.MaxValue);
            return Decode(units, terrainHeight);
        }

        private static float Decode(int units, float terrainHeight)
        {
            // 配列保存と同じfloat丸めを挟み、途中精度で境界判定を変えない
            // Round like the stored float array before multiplication to preserve boundary checks
            return (float)((float)(units / (double)ushort.MaxValue) * (double)terrainHeight);
        }

        private static void ValidateUnits(int units, float terrainHeight)
        {
            if (units >= 0 && units <= ushort.MaxValue && terrainHeight > 0f) return;
            string reason = $"Surface quantization outside r16 range: units={units}, terrainHeight={terrainHeight}.";
            Debug.LogError(reason);
            throw new InvalidOperationException(reason);
        }
    }
}
