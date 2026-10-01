using System;
using Client.Starter.EventMode;
using UnityEditor;

namespace Client.Tests.EventMode
{
    /// <summary>
    /// 出展モードのテストが環境変数を退避・有効化・復元する手順を一箇所に集める。有効化に要るキーが増えてもここだけ直せば全テストが揃う。
    /// ドメインリロードを跨ぐテストはSessionState経由の退避を、跨がないテストは配列での退避を使う。
    /// Gathers how exhibition-mode tests save, enable and restore env vars in one place, so a new enabling key is fixed here for every test.
    /// Tests crossing a domain reload save through SessionState; tests that do not save into an array.
    /// </summary>
    public static class EventModeTestEnvironment
    {
        // 出展モードの有効化に要るキー（本体とEditorのopt-in）
        // Keys needed to enable exhibition mode (the switch and the Editor opt-in)
        public static readonly string[] ExhibitionEnableKeys = { EventExhibitionSettings.EnableEnvKey, EventExhibitionSettings.EditorOptInEnvKey };

        // EventExhibitionSettingsが受理する唯一の有効値
        // The only enabled value EventExhibitionSettings accepts
        private const string EnabledValue = "1";

        // SessionStateはnullを持てないため、未設定を表す番兵を置く
        // SessionState cannot hold null, so a sentinel stands for "unset"
        private const string UnsetMarker = "\u0000unset";

        public static void EnableExhibitionMode()
        {
            foreach (var key in ExhibitionEnableKeys) Environment.SetEnvironmentVariable(key, EnabledValue);
        }

        public static void DisableExhibitionMode()
        {
            foreach (var key in ExhibitionEnableKeys) Environment.SetEnvironmentVariable(key, null);
        }

        public static string[] Capture(string[] keys)
        {
            var values = new string[keys.Length];
            for (var i = 0; i < keys.Length; i++) values[i] = Environment.GetEnvironmentVariable(keys[i]);
            return values;
        }

        public static void Restore(string[] keys, string[] values)
        {
            for (var i = 0; i < keys.Length; i++) Environment.SetEnvironmentVariable(keys[i], values[i]);
        }

        // ドメインリロードを跨いで戻せるよう、元の値をSessionStateへ置く
        // Saves the original values into SessionState so they can be restored across a domain reload
        public static void SaveToSession(string sessionKeyPrefix, string[] keys)
        {
            foreach (var key in keys) SessionState.SetString(sessionKeyPrefix + key, Environment.GetEnvironmentVariable(key) ?? UnsetMarker);
        }

        public static void RestoreFromSession(string sessionKeyPrefix, string[] keys)
        {
            foreach (var key in keys)
            {
                var previous = SessionState.GetString(sessionKeyPrefix + key, UnsetMarker);
                Environment.SetEnvironmentVariable(key, previous == UnsetMarker ? null : previous);
                SessionState.EraseString(sessionKeyPrefix + key);
            }
        }
    }
}
