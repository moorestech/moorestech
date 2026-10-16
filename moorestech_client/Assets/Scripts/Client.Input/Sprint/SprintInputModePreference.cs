using System;
using UniRx;
using UnityEngine;

namespace Client.Input
{
    /// <summary>
    /// 走りの操作方式を端末ごとに保存し、変化を通知する（言語設定と同じPlayerPrefs）
    /// Stores the sprint input mode per device and notifies changes (same PlayerPrefs as the language setting)
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

            // 壊れた保存値は既定へ戻し、開発者ログに残す
            // A corrupt saved value falls back to the default and is logged for developers
            Debug.LogWarning($"[SprintInputModePreference] Unknown saved sprint mode '{saved}'. Using Toggle.");
            return SprintInputMode.Toggle;
        }
    }
}
