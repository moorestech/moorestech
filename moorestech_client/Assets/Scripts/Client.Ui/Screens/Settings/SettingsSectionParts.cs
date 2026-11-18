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
            // Web の Title は見出しの字体で描く
            // The Web Title draws with the heading face
            var title = Create(key, "settings-section__title", UiTypographyClass.TitleOrder2);
            title.AddToClassList(UiTypographyClass.TitleHeadingFace);
            return title;
        }

        public static LocalizedLabel Caption(LocalizationKey key)
        {
            return Create(key, "settings-section__caption", UiTypographyClass.TextMd);
        }

        public static LocalizedLabel Note(string testid, LocalizationKey key)
        {
            var note = Create(key, "settings-section__note", UiTypographyClass.TextMd);
            note.name = testid;
            return note;
        }

        private static LocalizedLabel Create(LocalizationKey key, string className, string typographyClass)
        {
            // 画面のクラスは検索とテスト用、見た目は Typography のクラスが持つ
            // The screen class is for lookup and tests; the look comes from the Typography class
            var label = new LocalizedLabel();
            label.AddToClassList(className);
            label.AddToClassList(typographyClass);
            label.SetKey(key);
            return label;
        }
    }
}
