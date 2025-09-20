using UnityEditor;

namespace AltoEditor
{
    public static class EditorSettingsUtil
    {
        public static void SaveBool(string key, bool value)
        {
            EditorUserSettings.SetConfigValue(key, value ? "true" : "false");
        }

        public static bool LoadBool(string key, bool defaultValue = false)
        {
            string value = EditorUserSettings.GetConfigValue(key);
            if (value == null) { return defaultValue; }
            return (value == "true");
        }
    }
}
