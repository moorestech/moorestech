using System.Collections.Generic;
using Client.Ui.Components.Text;
using Client.Ui.Foundation.Input;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Text;
using Client.Ui.Foundation.Theme;
using Client.Ui.Screens.Tutorial.Pulse;
using Client.Ui.State.Tutorial.KeyControlHint;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Tutorial.KeyControlHint
{
    /// <summary>
    /// ホットバー直上に中央揃えで積むキー操作ヒント（Web KeyControlHintHud）
    /// Key control hints stacked centered right above the hotbar (Web KeyControlHintHud)
    /// </summary>
    public sealed class KeyControlHintView : VisualElement
    {
        private const string FloorOffsetProperty = "--key-control-hint-floor-offset";
        private const string HotbarGapProperty = "--key-control-hint-hotbar-gap";
        private const string RowGapProperty = "--key-control-hint-row-gap";

        private readonly List<ShortcutHintLabel> _rows = new();
        private readonly TutorialPulseBinding _pulse;
        private float _rowGap;

        public KeyControlHintView()
        {
            name = "key-control-hint-hud";
            AddToClassList("key-control-hint");
            pickingMode = PickingMode.Ignore;
            _pulse = new TutorialPulseBinding(this);
            RegisterCallback<CustomStyleResolvedEvent>(evt => OnCustomStyleResolved(evt.customStyle));
            UiDisplay.SetShown(this, false);
        }

        public void Render(KeyControlHintViewModel model)
        {
            // 行の数を ViewModel に合わせ、各行へ kbd と文を渡す
            // Match the row count to the view model and hand each row its kbd and sentence
            while (_rows.Count < model.Hints.Count) AddRow();
            while (model.Hints.Count < _rows.Count) RemoveLastRow();
            for (var i = 0; i < model.Hints.Count; i++) _rows[i].SetHint(model.Hints[i].Text, model.Hints[i].KeyName);
            ApplyRowGap();
            UiDisplay.SetShown(this, model.Hints.Count != 0);

            #region Internal

            void AddRow()
            {
                var row = new ShortcutHintLabel("key-control-hint", ShortcutHintLayout.Prefix);
                row.AddToClassList(UiTypographyClass.KeyHintText);
                row.AddToClassList("key-control-hint__item");
                Add(row);
                _rows.Add(row);
                _pulse.Attach(row, TutorialAttentionPulse.StrongScale);
                // pickingMode は継承されないので、部品の内側まで拾わなくする（Web pointer-events: none。D-36）
                // pickingMode is not inherited, so stop picking inside the part too (Web pointer-events: none; D-36)
                UiPickingIgnore.ApplyToSubtree(row);
            }

            void RemoveLastRow()
            {
                var row = _rows[_rows.Count - 1];
                _pulse.Detach(row);
                row.RemoveFromHierarchy();
                _rows.RemoveAt(_rows.Count - 1);
            }

            #endregion
        }

        private void OnCustomStyleResolved(ICustomStyle customStyle)
        {
            // bottom はホットバーの床と隙間の和（Web の calc）
            // bottom is the hotbar floor plus the gap (Web calc)
            if (UssCustomStyleReader.TryReadPixels(customStyle, FloorOffsetProperty, out var floorOffset) && UssCustomStyleReader.TryReadPixels(customStyle, HotbarGapProperty, out var hotbarGap))
            {
                style.bottom = floorOffset + hotbarGap;
            }
            if (UssCustomStyleReader.TryReadPixels(customStyle, RowGapProperty, out _rowGap)) ApplyRowGap();
        }

        private void ApplyRowGap()
        {
            // 行の間は Web の flex gap。2 行目以降の上の余白で写す
            // Rows are spaced like the Web flex gap, via the top margin of every row after the first
            for (var i = 0; i < _rows.Count; i++) _rows[i].style.marginTop = i == 0 ? 0f : _rowGap;
        }
    }
}
