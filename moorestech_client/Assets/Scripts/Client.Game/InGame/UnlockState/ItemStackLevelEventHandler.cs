using Client.Game.InGame.Context;
using Core.Item.Interface;
using MessagePack;
using Server.Event.EventReceive;
using VContainer.Unity;

namespace Client.Game.InGame.UnlockState
{
    // サーバーのスタックレベル解放イベントを購読しクライアントの状態を追随させる
    // Subscribe to server stack level unlock events and keep the client state in sync
    public class ItemStackLevelEventHandler : IInitializable
    {
        private readonly IItemStackLevelUnlocker _itemStackLevelUnlocker;

        public ItemStackLevelEventHandler(IItemStackLevelUnlocker itemStackLevelUnlocker)
        {
            _itemStackLevelUnlocker = itemStackLevelUnlocker;
        }

        public void Initialize()
        {
            ClientContext.VanillaApi.Event.SubscribeEventResponse(ItemStackLevelUnlockEventPacket.EventTag, OnStackLevelUnlocked);
        }

        private void OnStackLevelUnlocked(byte[] payload)
        {
            var data = MessagePackSerializer.Deserialize<ItemStackLevelUnlockEventPacket.ItemStackLevelMessagePack>(payload);
            _itemStackLevelUnlocker.UnlockStackLevel(data.ItemGuid, data.Level);
        }
    }
}
