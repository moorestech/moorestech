using Client.Ui.Components.Controls;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery.Entries.Controls
{
    // GameTextField の見本（GaugeInset 複数行の置き文字・本文、単行の検索欄、MantineInput）。寸法は Web の使い手の値
    // GameTextField samples (GaugeInset multiline with placeholder and with text, single-line search, MantineInput); sizes are the Web users' values
    public sealed class GameTextFieldGalleryEntry : IUiGalleryEntry
    {
        // バグ報告の記述欄 356×72・ビルドメニューの検索 452px・モーダルの入力 400px（Web の参照キャプチャの実寸）
        // Bug-report textarea 356x72, build-menu search 452px, modal input 400px (actual sizes in the Web reference captures)
        private const float TextAreaWidth = 356f;
        private const float TextAreaHeight = 72f;
        private const float SearchWidth = 452f;
        private const float MantineInputWidth = 400f;

        public UiGalleryFamily Family => UiGalleryFamily.Controls;
        public string ComponentName => "GameTextField";

        public VisualElement CreateSample()
        {
            var emptyArea = Sized(new GameTextField("gallery-text-area-empty", true, GameTextFieldLook.GaugeInset), TextAreaWidth, TextAreaHeight);
            emptyArea.SetPlaceholder(new UiText(LocalizationKeys.Ui.BugReport.Placeholder));
            var filledArea = Sized(new GameTextField("gallery-text-area-filled", true, GameTextFieldLook.GaugeInset), TextAreaWidth, TextAreaHeight);
            filledArea.SetValueWithoutNotify("ベルトが止まった。\nIt stopped after placing a splitter.");

            // 複数行 2 つの下に単行の検索欄と Mantine の名前欄を縦に積む
            // Stack the single-line search and the Mantine name field under the two multiline fields
            var sample = new VisualElement();
            var areas = new VisualElement();
            areas.style.flexDirection = FlexDirection.Row;
            areas.Add(emptyArea);
            areas.Add(filledArea);
            sample.Add(areas);
            sample.Add(Sized(new GameTextField("gallery-search", false, GameTextFieldLook.GaugeInset), SearchWidth, float.NaN));
            sample.Add(Sized(new GameTextField("gallery-mantine-input", false, GameTextFieldLook.MantineInput), MantineInputWidth, float.NaN));
            return sample;

            #region Internal

            GameTextField Sized(GameTextField field, float width, float height)
            {
                field.style.width = width;
                if (!float.IsNaN(height)) field.style.height = height;
                field.style.marginRight = 16;
                field.style.marginBottom = 12;
                return field;
            }

            #endregion
        }
    }
}
