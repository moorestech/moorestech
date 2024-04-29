using System.Collections;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Core.Item.Config;
using Core.Item.Interface.Config;
using Game.Context;
using UnityDebugSheet.Runtime.Core.Scripts;

namespace Client.DebugSystem
{
    public class ItemGetDebugSheet : DefaultDebugPageBase
    {
        protected override string Title => "Get Item";

        public override IEnumerator Initialize()
        {
            IReadOnlyList<IItemConfigData> items = ServerContext.ItemConfig.ItemConfigDataList;
            foreach (var itemConfig in items)
            {
                var itemImage = MoorestechContext.ItemImageContainer.GetItemView(itemConfig.ItemId);
                var subText = $"Count:{itemConfig.MaxStack}";

                AddButton(itemImage.ItemName, subText, icon: itemImage.ItemImage, clicked: () =>
                {
                    var playerId = MoorestechContext.PlayerConnectionSetting.PlayerId;
                    var command = $"give {playerId} {itemConfig.ItemId} {itemConfig.MaxStack}";
                    MoorestechContext.VanillaApi.SendOnly.SendCommand(command);
                });
            }

            yield break;
        }
    }
}