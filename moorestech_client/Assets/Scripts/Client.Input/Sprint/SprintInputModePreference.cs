using System;
using UniRx;
using UnityEngine;

namespace Client.Input
{
    /// <summary>
    /// - 端末ごとの走り方式保存
    /// - 言語設定と同じ保存先
    /// - 方式の変化通知
    /// - Stores sprint mode per device
    /// - Uses the same store as language settings
    /// - Notifies mode changes
    /// </summary>
    public static class SprintInputModePreference
    {
        internal const string PreferenceKey = "SprintInputMode";
        private static readonly Subject<SprintInputMode> SprintInputModeChangedSubject = new();

        public static IObservable<SprintInputMode> OnSprintInputModeChanged => SprintInputModeChangedSubject;

        public static SprintInputMode Current => Load();

        public static void SetMode(SprintInputMode mode)
        {
            if (Load() == mode) return;
            PlayerPrefs.SetString(PreferenceKey, mode.ToString());
            PlayerPrefs.Save();
            SprintInputModeChangedSubject.OnNext(mode);
        }

        private static SprintInputMode Load()
        {
            // 未保存は既定のトグル（ADR 0082）
            // Unsaved means the default toggle (ADR 0082)
            if (!PlayerPrefs.HasKey(PreferenceKey)) return SprintInputMode.Toggle;

            // TryParseは"2"等の数値文字列も通すため、定義域に閉じているかも確かめる
            // TryParse also accepts numeric strings like "2", so check the value is within the defined set
            var saved = PlayerPrefs.GetString(PreferenceKey);
            if (Enum.TryParse(saved, out SprintInputMode mode) && Enum.IsDefined(typeof(SprintInputMode), mode)) return mode;

            // 不正保存値は既定へ戻しログに残す
            // Fall back to the default for invalid saved values and log it
            Debug.LogWarning($"[SprintInputModePreference] Unknown saved sprint mode '{saved}'. Using Toggle.");
            return SprintInputMode.Toggle;
        }
    }
}
