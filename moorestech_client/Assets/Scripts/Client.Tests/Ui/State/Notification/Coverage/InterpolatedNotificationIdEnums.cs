using System.Collections.Generic;

namespace Client.Tests.Ui.State.Notification.Coverage
{
    /// <summary>
    /// 補間で作られる通知 id の接頭辞と、展開する enum・その経路では送られない値（Web の interpolatedIdEnums と同じ表）
    /// Prefixes of interpolated notification ids with the enum to expand and the values that path never sends (same table as Web interpolatedIdEnums)
    /// </summary>
    internal static class InterpolatedNotificationIdEnums
    {
        public static readonly IReadOnlyDictionary<string, InterpolatedIdEnum> ByPrefix = new Dictionary<string, InterpolatedIdEnum>
        {
            ["denied.blueprint."] = new("BlueprintFailureReason", "None", "InvalidName", "EmptyArea", "UnknownOperation"),
            ["denied.blueprintCreate."] = new("BlueprintCreateFailure", "None", "NotUnlocked"),
            ["denied.railEdit."] = new("RailConnectionEditFailureReason", "None"),
            ["denied.electricWireExtend."] = new("ElectricWirePlacementFailureReason", "InventoryFull", "NotConnected"),
            ["denied.electricWireDisconnect."] = new("ElectricWirePlacementFailureReason", "None", "OutOfRange", "AlreadyConnected", "ConnectionLimit", "NoWireItem", "NoPoleItem", "PositionOccupied", "InvalidMode", "NotUnlocked", "InsufficientItems"),
            ["denied.gearChainDisconnect."] = new("GearChainDisconnectFailureReason", "None"),
            ["denied.gearChainConnect."] = new("GearChainPlacementFailureReason", "None", "NoPoleItem", "PositionOccupied", "InsufficientItems"),
        };

        internal sealed class InterpolatedIdEnum
        {
            public readonly string EnumName;
            public readonly IReadOnlyList<string> NotSentMembers;

            public InterpolatedIdEnum(string enumName, params string[] notSentMembers)
            {
                EnumName = enumName;
                NotSentMembers = notSentMembers;
            }
        }
    }
}
