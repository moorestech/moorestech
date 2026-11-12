using System;
using Client.Ui.State.BlockInventory.Special.Details;

namespace Client.Ui.State.BlockInventory.Special
{
    /// <summary>
    /// 特殊族の区画を出すか（Web の各 Section が null を返す条件）。区画は詳細が届いたときだけ、本文と題の操作は常に出す（中身の出し入れは View が持つ）
    /// Whether a special-family section shows (the condition each Web Section returns null on); sections only with their detail, bodies and the title action always (their inner toggles stay in the view)
    /// </summary>
    public static class SpecialDetailVisibility
    {
        public static bool Shows(SpecialDetailSection section, SpecialBlockDetailViewModel detail)
        {
            switch (section)
            {
                case SpecialDetailSection.Gear: return detail.TryGetGear(out _);
                case SpecialDetailSection.GearToElectric: return detail.TryGetGearToElectric(out _);
                case SpecialDetailSection.ElectricNetwork: return detail.TryGetElectricNetwork(out _);
                case SpecialDetailSection.GearNetwork: return detail.TryGetGearNetwork(out _);
                case SpecialDetailSection.Body: return true;
                case SpecialDetailSection.TitleAction: return true;
                default: throw new ArgumentOutOfRangeException(nameof(section), section, null);
            }
        }
    }
}
