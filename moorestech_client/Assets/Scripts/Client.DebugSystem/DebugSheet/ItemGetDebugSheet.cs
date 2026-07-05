using System.Collections;
using Client.Game.InGame.Context;
using Core.Item;
using Core.Master;
using Server.Protocol.PacketResponse;
using UnityDebugSheet.Runtime.Core.Scripts;

namespace Client.DebugSystem
{
    public class ItemGetDebugSheet : DefaultDebugPageBase
    {
        protected override string Title => "Get Item";
        
        public override IEnumerator Initialize()
        {
            var itemIds = MasterHolder.ItemMaster.GetItemAllIds();
            foreach (var itemId in itemIds)
            {
                //TODO: あとでItemImageContainer.GetItemViewの引数をItemIdにする
                var itemImage = ClientContext.ItemImageContainer.GetItemView(itemId);
                var subText = $"Count:{ItemStackLevelDataStore.Instance.GetMaxStack(itemId)}";

                AddButton(itemImage.ItemName, subText, icon: itemImage.ItemImage, clicked: () =>
                {
                    // クリック時点で最新の解放済み上限を再評価する（実行中の解放を反映）
                    // Re-evaluate the current unlocked max stack at click time to reflect runtime unlocks
                    var maxStack = ItemStackLevelDataStore.Instance.GetMaxStack(itemId);
                    var playerId = ClientContext.PlayerConnectionSetting.PlayerId;
                    var command = $"{SendCommandProtocol.GiveCommand} {playerId} {itemId} {maxStack}";
                    ClientContext.VanillaApi.SendOnly.SendCommand(command);
                });
            }
            
            yield break;
        }
    }
}