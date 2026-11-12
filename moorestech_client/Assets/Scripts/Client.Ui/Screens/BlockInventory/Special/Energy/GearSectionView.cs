using System.Collections.Generic;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Text;
using Client.Ui.Screens.BlockInventory.Special.Registration;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.Items;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Special.Energy
{
    /// <summary>
    /// 歯車の区画（トルク・RPM の 2 行。Web GearSection）
    /// Gear section with torque and RPM rows (Web GearSection)
    /// </summary>
    public sealed class GearSectionView : VisualElement, ISpecialSectionRenderer<GearDetail>
    {
        private readonly LocalizedLabel _torque = SpecialViewParts.Label("gear-torque", UiTypographyClass.TextSm, UiTypographyClass.TextDefault);
        private readonly LocalizedLabel _rpm = SpecialViewParts.Label("gear-rpm", UiTypographyClass.TextSm, UiTypographyClass.TextDefault);

        public GearSectionView()
        {
            name = "gear-section";
            AddToClassList("special-section");
            var stack = new UiStack(UiGap.Px2);
            stack.AddItem(_torque);
            stack.AddItem(_rpm);
            Add(stack);
        }

        public void Render(IReadOnlyList<SlotItem> itemSlots, GearDetail gear)
        {
            var rows = GearRowDisplay.From(gear);
            _torque.SetText(rows.Torque);
            _rpm.SetText(rows.Rpm);
        }
    }
}
