using System.Collections;
using Client.Game.InGame.Context;
using Game.Context;
using UnityDebugSheet.Runtime.Core.Scripts;

namespace Client.DebugSystem
{
    public class ItemGetDebugSheet : DefaultDebugPageBase
    {
        protected override string Title => "Get Item";
        
        public override IEnumerator Initialize()
        {
            var items = ServerContext.ItemConfig.ItemConfigDataList;
            foreach (var itemConfig in items)
            {
                var itemImage = ClientContext.ItemImageContainer.GetItemView(itemConfig.ItemId);
                var subText = $"Count:{itemConfig.MaxStack}";
                
                AddButton(itemImage.ItemName, subText, icon: itemImage.ItemImage, clicked: () =>
                {
                    var playerId = ClientContext.PlayerConnectionSetting.PlayerId;
                    var command = $"give {playerId} {itemConfig.ItemId} {itemConfig.MaxStack}";
                    ClientContext.VanillaApi.SendOnly.SendCommand(command);
                });
            }
            
            yield break;
        }
    }
}