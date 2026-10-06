using System;
using Game.MapGeneration.Facade.Surface;
using Game.MapGeneration.Pipeline.Config;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Surface
{
    public static class SurfaceQuantization
    {
        public const int TerrainStorageSteps = 32766;
        private const float TerrainStorageReciprocal = 1f / TerrainStorageSteps;

        public static float LandFloor(TerrainGenerationConfig config, SurfaceEnvelope envelope, string tile)
        {
            double minimum = (double)envelope.SeaY + envelope.MaximumWaveRise + envelope.LandClearance;
            ValidateHeight(config, tile);
            int units = (int)Math.Ceiling(minimum / config.terrainHeight * TerrainStorageSteps);

            // Unity格納後のfloat高さで陸地下限を満たす
            // Satisfy the land lower bound using the float height stored by Unity
            while (units <= TerrainStorageSteps && Decode(units, config.terrainHeight) < minimum) units++;
            ValidateUnits(units, config, tile);
            return Decode(units, config.terrainHeight);
        }

        public static float PadHeight(int boxBottom, TerrainGenerationConfig config, string tile)
        {
            double maximum = boxBottom - 0.001d;
            ValidateHeight(config, tile);
            int units = (int)Math.Floor(maximum / config.terrainHeight * TerrainStorageSteps);

            // 採掘底面の余裕はr16読込後のUnity格納値で判定する
            // Evaluate the mining clearance against Unity storage after the r16 reload
            while (units >= 0 && Decode(units, config.terrainHeight) > maximum) units--;
            ValidateUnits(units, config, tile);
            return Decode(units, config.terrainHeight);
        }

        public static float EncodeNormalized(float normalizedHeight)
        {
            int storageUnits = Mathf.Clamp(Mathf.RoundToInt(normalizedHeight * TerrainStorageSteps), 0, TerrainStorageSteps);

            // 格納格子をr16で運び、SetHeightsで同じ格子へ戻す
            // Carry the storage lattice through r16 so SetHeights restores the same lattice point
            int fileUnits = Mathf.RoundToInt(storageUnits / (float)TerrainStorageSteps * ushort.MaxValue);
            return fileUnits / (float)ushort.MaxValue;
        }

        public static float StoredNormalized(float encodedHeight)
        {
            int units = Mathf.Clamp(Mathf.RoundToInt(encodedHeight * TerrainStorageSteps), 0, TerrainStorageSteps);
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
            if (SurfaceGenerationValidation.Finite(config.terrainHeight) && config.terrainHeight > 0f) return;
            throw SurfaceGenerationValidation.Failure(config, tile, "Surface quantization requires a finite positive terrainHeight.");
        }

        private static void ValidateUnits(int units, TerrainGenerationConfig config, string tile)
        {
            if (units >= 0 && units <= TerrainStorageSteps) return;
            throw SurfaceGenerationValidation.Failure(config, tile,
                $"Surface quantization outside TerrainData range: units={units}, terrainHeight={config.terrainHeight}.");
        }
    }
}
