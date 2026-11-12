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
    public sealed class GearSectionView : VisualElement, ISpecialDetailRenderer
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

        public void Render(IReadOnlyList<SlotItem> itemSlots, SpecialBlockDetailViewModel detail)
        {
            // Presenter は SpecialDetailVisibility が真（詳細あり）のときだけ呼ぶので、無いのは表と View の結線の誤り。隠れたまま古い値を残さず知らせる
            // The Presenter calls only when SpecialDetailVisibility is true (detail present), so a missing detail is a table/View wiring mistake; report it instead of leaving stale values
            if (!detail.TryGetGear(out var gear))
            {
                UnityEngine.Debug.LogError("[GearSectionView] Render called without gear detail; SpecialDetailVisibility and the view disagree");
                return;
            }
            var rows = GearRowDisplay.From(gear);
            _torque.SetText(rows.Torque);
            _rpm.SetText(rows.Rpm);
        }
    }
}
