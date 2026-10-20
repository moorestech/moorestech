using System;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.GraphicsQuality
{
    /// <summary>
    /// - 端末ごとの画質設定の保存
    /// - 走り方式と同じ保存先
    /// - 値の変化通知
    /// - Stores graphics quality per device
    /// - Uses the same store as the sprint mode
    /// - Notifies value changes
    /// </summary>
    public static class GraphicsQualityPreference
    {
        internal const string PreferenceKey = "GraphicsQuality";
        private const char Separator = '|';
        private const int FieldCount = 9;
        private static readonly Subject<GraphicsQualitySettings> SettingsChangedSubject = new();

        public static IObservable<GraphicsQualitySettings> OnSettingsChanged => SettingsChangedSubject;
        public static GraphicsQualitySettings Current => Load();

        public static void ApplyPreset(GraphicsQualityPreset preset)
        {
            SetSettings(GraphicsQualityPresets.Get(preset));
        }

        public static void SetSettings(GraphicsQualitySettings settings)
        {
            var changed = !Load().Equals(settings);
            PlayerPrefs.SetString(PreferenceKey, Serialize(settings));
            PlayerPrefs.Save();
            // silent-ok: 同値の再保存は画面に変化が無い / 保存テストで再通知が無いことを確認する
            if (!changed) return;
            SettingsChangedSubject.OnNext(settings);
        }

        internal static string Serialize(GraphicsQualitySettings s)
        {
            return string.Join(Separator, s.DrawDistance, s.ShadowsEnabled, s.RenderScale, s.SsaoEnabled, s.PostProcessingEnabled,
                s.EffectsEnabled, s.MapObjectsVisible, s.TerrainDetail, s.OceanVisible);
        }

        private static GraphicsQualitySettings Load()
        {
            // 未保存は既定の中（裁定 2026-10-20）
            // Unsaved means the default medium (ruling 2026-10-20)
            var medium = GraphicsQualityPresets.Get(GraphicsQualityPreset.Medium);
            if (!PlayerPrefs.HasKey(PreferenceKey)) return medium;

            var saved = PlayerPrefs.GetString(PreferenceKey);
            if (TryDeserialize(saved, out var settings)) return settings;

            // 不正値は中へ修復し、開発者が読めるログへ残す
            // Repair an invalid value to medium and leave a log developers can read
            Debug.LogWarning($"[GraphicsQualityPreference] Unknown saved graphics quality '{saved}'. Using Medium.");
            PlayerPrefs.SetString(PreferenceKey, Serialize(medium));
            PlayerPrefs.Save();
            return medium;

            #region Internal

            bool TryDeserialize(string text, out GraphicsQualitySettings result)
            {
                result = medium;
                var fields = text.Split(Separator);
                if (fields.Length != FieldCount) return false;
                if (!TryEnum(fields[0], out GraphicsDrawDistance drawDistance) || !bool.TryParse(fields[1], out var shadows) ||
                    !TryEnum(fields[2], out GraphicsRenderScale renderScale) || !bool.TryParse(fields[3], out var ssao) ||
                    !bool.TryParse(fields[4], out var post) || !bool.TryParse(fields[5], out var effects) ||
                    !bool.TryParse(fields[6], out var mapObjects) || !TryEnum(fields[7], out GraphicsTerrainDetail terrain) ||
                    !bool.TryParse(fields[8], out var ocean)) return false;
                result = new GraphicsQualitySettings(drawDistance, shadows, renderScale, ssao, post, effects, mapObjects, terrain, ocean);
                return true;
            }

            bool TryEnum<TEnum>(string text, out TEnum value) where TEnum : struct, Enum
            {
                // TryParseは"7"等の数値文字列も通すため定義域も確かめる
                // TryParse also accepts numeric strings like "7", so check the defined set too
                return Enum.TryParse(text, out value) && Enum.IsDefined(typeof(TEnum), value);
            }

            #endregion
        }
    }
}
