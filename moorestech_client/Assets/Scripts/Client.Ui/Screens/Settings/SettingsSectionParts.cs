using Client.Ui.Foundation.Text;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Settings
{
    /// <summary>
    /// 設定の各節が共有する見出し・項目名・注記の行（Web の Title order=2・Text・role=status）
    /// Heading, item caption and notice rows shared by the settings sections (Web Title order=2, Text and role=status)
    /// </summary>
    public static class SettingsSectionParts
    {
        public static LocalizedLabel Title(LocalizationKey key)
        {
            return Create("settings-section__title", key);
        }

        public static LocalizedLabel Caption(LocalizationKey key)
        {
            return Create("settings-section__caption", key);
        }

        public static LocalizedLabel Note(string testid, LocalizationKey key)
        {
            var note = Create("settings-section__note", key);
            note.name = testid;
            return note;
        }

        public static void ShowIf(VisualElement element, bool visible)
        {
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static LocalizedLabel Create(string className, LocalizationKey key)
        {
            var label = new LocalizedLabel();
            label.AddToClassList(className);
            label.SetKey(key);
            return label;
        }
    }
}
