using System;
using Client.Ui.State.BlockInventory.Special.Details;

namespace Client.Ui.State.BlockInventory.Special
{
    /// <summary>
    /// 特殊族の区画を出すか。区画は詳細が届いたときだけ
    /// Whether a special section shows; only with its detail
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
