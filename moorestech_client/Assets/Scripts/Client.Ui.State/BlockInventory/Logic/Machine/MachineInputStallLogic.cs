using System;
using Server.Protocol.PacketResponse;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.State.BlockInventory.Logic.Machine
{
    /// <summary>
    /// 止まった理由ごとの文言キー（Web machineInputStallLogic.ts の写し）
    /// The message key per stall reason (port of Web machineInputStallLogic.ts)
    /// </summary>
    public static class MachineInputStallLogic
    {
        public static LocalizationKey MessageKey(MachineInputStallReasonKind reason)
        {
            return reason switch
            {
                MachineInputStallReasonKind.MixedLevel => LocalizationKeys.Ui.BlockInventory.MachineInputStallMixedLevel,
                MachineInputStallReasonKind.AboveCraftCap => LocalizationKeys.Ui.BlockInventory.MachineInputStallAboveCraftCap,
                _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null),
            };
        }
    }
}
