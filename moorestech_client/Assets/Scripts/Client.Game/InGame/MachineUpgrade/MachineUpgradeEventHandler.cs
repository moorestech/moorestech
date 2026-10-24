using Client.Network.API;
using MessagePack;
using Server.Event.EventReceive.MachineUpgrade;
using VContainer.Unity;

namespace Client.Game.InGame.MachineUpgrade
{
    /// <summary>
    ///     スロット上限と製作上限のイベントを購読してミラーへ反映する
    ///     Subscribes to the slot-limit and craft-cap events and applies them to the mirrors
    /// </summary>
    public class MachineUpgradeEventHandler : IInitializable
    {
        private readonly IVanillaApiEvent _vanillaApiEvent;
        private readonly ClientModuleSlotStackLimitDatastore _moduleSlotStackLimitDatastore;
        private readonly ClientLevelInheritanceCraftCapDatastore _levelInheritanceCraftCapDatastore;

        public MachineUpgradeEventHandler(IVanillaApiEvent vanillaApiEvent, ClientModuleSlotStackLimitDatastore moduleSlotStackLimitDatastore, ClientLevelInheritanceCraftCapDatastore levelInheritanceCraftCapDatastore)
        {
            _vanillaApiEvent = vanillaApiEvent;
            _moduleSlotStackLimitDatastore = moduleSlotStackLimitDatastore;
            _levelInheritanceCraftCapDatastore = levelInheritanceCraftCapDatastore;
        }

        public void Initialize()
        {
            _vanillaApiEvent.SubscribeEventResponse(ModuleSlotStackLevelEventPacket.EventTag, OnModuleSlotStackLevelChanged);
            _vanillaApiEvent.SubscribeEventResponse(LevelInheritanceCraftCapUnlockEventPacket.EventTag, OnCraftCapUnlocked);
        }

        private void OnModuleSlotStackLevelChanged(byte[] payload)
        {
            var data = MessagePackSerializer.Deserialize<ModuleSlotStackLevelEventPacket.ModuleSlotStackLevelMessagePack>(payload);
            _moduleSlotStackLimitDatastore.Apply(data.Level);
        }

        private void OnCraftCapUnlocked(byte[] payload)
        {
            var data = MessagePackSerializer.Deserialize<LevelInheritanceCraftCapUnlockEventPacket.LevelInheritanceCraftCapMessagePack>(payload);
            _levelInheritanceCraftCapDatastore.Apply(data.FamilyBaseItemGuid, data.Level);
        }
    }
}
