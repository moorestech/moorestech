using System;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.State.BlockInventory.Logic.Machine
{
    // 機械の入力が止まった理由（Web の MachineInputStallFetchedData.reason）
    // Why a machine's input stalled (Web MachineInputStallFetchedData.reason)
    public enum MachineInputStallReason
    {
        MixedLevel,
        AboveCraftCap,
    }

    /// <summary>
    /// 止まった理由ごとの文言キー（Web machineInputStallLogic.ts の写し）
    /// The message key per stall reason (port of Web machineInputStallLogic.ts)
    /// </summary>
    public static class MachineInputStallLogic
    {
        public static LocalizationKey MessageKey(MachineInputStallReason reason)
        {
            return reason switch
            {
                MachineInputStallReason.MixedLevel => LocalizationKeys.Ui.BlockInventory.MachineInputStallMixedLevel,
                MachineInputStallReason.AboveCraftCap => LocalizationKeys.Ui.BlockInventory.MachineInputStallAboveCraftCap,
                _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null),
            };
        }
    }
}
