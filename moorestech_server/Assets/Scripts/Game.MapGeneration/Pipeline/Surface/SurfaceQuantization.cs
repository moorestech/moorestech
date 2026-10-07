using System;
using Game.MapGeneration.Surface;
using Game.MapGeneration.Pipeline.Config;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Surface
{
    public static class SurfaceQuantization
    {
        private const float TerrainStorageReciprocal = 1f / TerrainHeightStorage.Steps;

        public static float LandFloor(TerrainGenerationConfig config, SurfaceEnvelope envelope, string tile)
        {
            double minimum = (double)envelope.SeaY + envelope.MaximumWaveRise + envelope.LandClearance;
            ValidateHeight(config, tile);
            int units = (int)Math.Ceiling(minimum / config.terrainHeight * TerrainHeightStorage.Steps);

            // 格納後float高さで陸地下限を満たす
            // Meet the land floor with the stored float height
            while (units <= TerrainHeightStorage.Steps && Decode(units, config.terrainHeight) < minimum) units++;
            ValidateUnits(units, config, tile);
            return Decode(units, config.terrainHeight);
        }

        // 陸地下限+採掘底面余裕+1段の整数下限
        // Integer lower bound: land floor + one storage step + mining clearance
        public static double MinimumMiningBottom(TerrainGenerationConfig config, SurfaceEnvelope envelope, string tile)
        {
            double quantum = (double)config.terrainHeight / TerrainHeightStorage.Steps;
            return Math.Ceiling(LandFloor(config, envelope, tile) + quantum + TerrainHeightStorage.MiningBottomClearanceMeters);
        }

        public static float PadHeight(int boxBottom, TerrainGenerationConfig config, string tile)
        {
            double maximum = boxBottom - TerrainHeightStorage.MiningBottomClearanceMeters;
            ValidateHeight(config, tile);
            int units = (int)Math.Floor(maximum / config.terrainHeight * TerrainHeightStorage.Steps);

            // 採掘底面の余裕はr16読込後のUnity格納値で判定する
            // Evaluate the mining clearance against Unity storage after the r16 reload
            while (0 <= units && maximum < Decode(units, config.terrainHeight)) units--;
            ValidateUnits(units, config, tile);
            return Decode(units, config.terrainHeight);
        }

        // 正規化高さをr16整数段へ丸める
        // Round a normalized height to an r16 integer step
        public static int ToR16Units(float normalizedHeight)
        {
            return Mathf.Clamp(Mathf.RoundToInt(normalizedHeight * ushort.MaxValue), 0, ushort.MaxValue);
        }

        public static float RoundTripR16(float normalizedHeight)
        {
            return ToR16Units(normalizedHeight) / (float)ushort.MaxValue;
        }

        public static float EncodeNormalized(float normalizedHeight)
        {
            int storageUnits = Mathf.Clamp(Mathf.RoundToInt(normalizedHeight * TerrainHeightStorage.Steps), 0, TerrainHeightStorage.Steps);

            // 格納格子をr16で運び戻す
            // Carry the storage lattice via r16 and restore it with SetHeights
            int fileUnits = Mathf.RoundToInt(storageUnits / (float)TerrainHeightStorage.Steps * ushort.MaxValue);
            return fileUnits / (float)ushort.MaxValue;
        }

        public static float StoredNormalized(float encodedHeight)
        {
            int units = Mathf.Clamp(Mathf.RoundToInt(encodedHeight * TerrainHeightStorage.Steps), 0, TerrainHeightStorage.Steps);
            return DecodeNormalized(units);
        }

        private static float Decode(int units, float terrainHeight)
        {
            // Unity読戻しのfloat丸めを確定してからメートルへ換算する
            // Fix the Unity readback float rounding before converting to meters
            return (float)((double)DecodeNormalized(units) * terrainHeight);
        }

        private static float DecodeNormalized(int units)
        {
            // 実測した読戻しは除算でなくfloat逆数との積になる
            // Measured readback multiplies by the float reciprocal instead of dividing
            return (float)(units * (double)TerrainStorageReciprocal);
        }

        private static void ValidateHeight(TerrainGenerationConfig config, string tile)
        {
            if (SurfaceGenerationValidation.Finite(config.terrainHeight) && 0f < config.terrainHeight) return;
            throw SurfaceGenerationValidation.Failure(config, tile, "Surface quantization requires a finite positive terrainHeight.");
        }

        private static void ValidateUnits(int units, TerrainGenerationConfig config, string tile)
        {
            if (0 <= units && units <= TerrainHeightStorage.Steps) return;
            throw SurfaceGenerationValidation.Failure(config, tile,
                $"Surface quantization outside TerrainData range: units={units}, terrainHeight={config.terrainHeight}.");
        }
    }
}
