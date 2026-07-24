using System;
using System.Collections.Generic;
using UnityEngine;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;

namespace Game.MapGeneration.Pipeline.Runtime
{
    // GenerationModule（生成型）→ 実行時 POCO の小さな値変換ヘルパー群。
    // enum・GUID・PlacementNoise/Filter・biomes ビットマスクの変換を一箇所に集約する。
    // Small value-conversion helpers from the generated GenerationModule to runtime POCOs,
    // centralizing enum/GUID/PlacementNoise/Filter/biomes-bitmask conversions.
    internal static class RuntimeConvert
    {
        // Mooresmaster は enum をオプション名の文字列で生成するため、名前で POCO enum へパースする。
        // 不正名はサイレント既定化せず、違反名を添えて即例外にする（マスタデータ防御）。
        // Mooresmaster emits enums as option-name strings, so parse by name into the POCO enums.
        // Invalid names fail loud with the offending value instead of silently defaulting.
        public static MapNoiseType ToMapNoiseType(string generatedNoiseType) =>
            ParseEnum<MapNoiseType>(generatedNoiseType, "noiseType");
        public static NoiseOp ToNoiseOp(string generatedNoiseOp) =>
            ParseEnum<NoiseOp>(generatedNoiseOp, "noiseOp");
        public static SecondaryPlacementMode ToSecondaryMode(string generatedMode) =>
            ParseEnum<SecondaryPlacementMode>(generatedMode, "secondaryPlacementMode");

        static T ParseEnum<T>(string name, string fieldName) where T : struct
        {
            if (Enum.TryParse<T>(name, out var v)) return v;
            throw new InvalidOperationException(
                $"[GenerationRuntimeConfig] '{fieldName}' has an unrecognized enum value: '{name}' (expected a {typeof(T).Name} option name).");
        }

        // uuid(Guid) 配列 → mapObjectGuid 文字列配列（空要素はそのまま保持）。
        // uuid(Guid) array to mapObjectGuid string array (preserving element order).
        public static string[] ToGuidStrings<T>(T[] elements, Func<T, Guid> selector)
        {
            if (elements == null) return new string[0];
            var result = new string[elements.Length];
            for (int i = 0; i < elements.Length; i++)
                result[i] = selector(elements[i]).ToString();
            return result;
        }

        // biomes 列挙配列（None/Grassland.../Woods、宣言順 0..8）を BiomeFlags ビットマスクへ合成する。
        // Compose the biomes enum array (None/Grassland.../Woods, order 0..8) into a BiomeFlags bitmask.
        public static BiomeFlags ToBiomeFlags(string[] biomes)
        {
            var flags = BiomeFlags.None;
            if (biomes == null) return flags;
            foreach (var b in biomes)
                flags |= NameToFlag(b);
            return flags;
        }

        static BiomeFlags NameToFlag(string name)
        {
            switch (name)
            {
                case "Grassland": return BiomeFlags.Grassland;
                case "Forest": return BiomeFlags.Forest;
                case "Savanna": return BiomeFlags.Savanna;
                case "Desert": return BiomeFlags.Desert;
                case "Mesa": return BiomeFlags.Mesa;
                case "Alpine": return BiomeFlags.Alpine;
                case "Jungle": return BiomeFlags.Jungle;
                case "Woods": return BiomeFlags.Woods;
                default: return BiomeFlags.None; // None（構造バイオームは対象外）
            }
        }

        // resolutionPreset 生成 enum（_256/_512/_1024/_2048、宣言順 0..3）→ POCO enum。
        // resolutionPreset generated enum (_256/_512/_1024/_2048, order 0..3) to POCO enum.
        public static TerrainResolutionPreset ToResolutionPreset(string name) =>
            ParseEnum<TerrainResolutionPreset>(name, "resolutionPreset");

        // keyframe 配列 → UnityEngine.AnimationCurve（空配列は null＝線形）。
        // keyframe array to UnityEngine.AnimationCurve (empty array yields null = linear).
        public static AnimationCurve ToAnimationCurve<T>(
            T[] keyframes,
            Func<T, float> time, Func<T, float> value,
            Func<T, float> inTangent, Func<T, float> outTangent)
        {
            if (keyframes == null || keyframes.Length == 0) return null;
            var keys = new List<Keyframe>(keyframes.Length);
            foreach (var k in keyframes)
                keys.Add(new Keyframe(time(k), value(k), inTangent(k), outTangent(k)));
            return new AnimationCurve(keys.ToArray());
        }
    }
}
