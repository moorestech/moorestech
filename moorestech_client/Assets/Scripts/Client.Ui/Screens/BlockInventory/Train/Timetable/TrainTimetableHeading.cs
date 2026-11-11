using Client.Ui.Foundation.Text;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Train.Timetable
{
    /// <summary>
    /// 枠の見出し。内側余白は器に、文字の型（sm・高コントラスト）はラベルに付ける（同じ要素に付けると padding が取り合う）
    /// Frame heading: padding on the wrapper and the type (sm, high contrast) on the label, since both on one element fight over padding
    /// </summary>
    public static class TrainTimetableHeading
    {
        public static VisualElement Create(LocalizationKey key)
        {
            var wrapper = new VisualElement();
            wrapper.AddToClassList("train-timetable__section-heading");
            var label = new LocalizedLabel();
            label.SetKey(key);
            label.AddToClassList(UiTypographyClass.TextSm);
            label.AddToClassList(UiTypographyClass.TextHighContrast);
            wrapper.Add(label);
            return wrapper;
        }
    }
}
