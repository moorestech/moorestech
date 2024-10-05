using System.Collections;
using System.Linq;
using Client.Game.InGame.Context;
using Core.Master;
using Game.Context;
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
                var itemElement = MasterHolder.ItemMaster.GetItemMaster(itemId);
                //TODO: あとでItemImageContainer.GetItemViewの引数をItemIdにする
                var itemImage = ClientContext.ItemImageContainer.GetItemView(itemId); 
                var subText = $"Count:{itemElement.MaxStack}";
                
                AddButton(itemImage.ItemName, subText, icon: itemImage.ItemImage, clicked: () =>
                {
                    var playerId = ClientContext.PlayerConnectionSetting.PlayerId;
                    var command = $"give {playerId} {itemId} {itemElement.MaxStack}";
                    ClientContext.VanillaApi.SendOnly.SendCommand(command);
                });
            }
            
            yield break;
        }
    }
}