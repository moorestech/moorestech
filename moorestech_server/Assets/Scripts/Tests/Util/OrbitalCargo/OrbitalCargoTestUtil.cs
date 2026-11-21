using System.Collections.Generic;
using Core.Inventory;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.UnlockState;
using Game.UnlockState.States;
using NUnit.Framework;

namespace Tests.Util.OrbitalCargo
{
    // 積荷の選択を使うテストの補助
    // Helpers for tests that use the orbital cargo selection
    public static class OrbitalCargoTestUtil
    {
        // 枠0の返却先。選び直しで返す品はすべてスロットに残る
        // A zero-slot refund target; everything a reselection refunds stays in its slot
        public static IOpenableInventory NoRefundInventory()
        {
            return new OpenableInventoryItemDataStoreService((_, _) => { }, ServerContext.ItemStackFactory, 0);
        }

        public static void SelectCargo(IBlock block, ItemId payloadItemId)
        {
            var result = block.GetComponent<IOrbitalCargoSelectorComponent>().SelectCargo(payloadItemId, NoRefundInventory());
            Assert.AreEqual(OrbitalCargoSelectionResult.Success, result, $"積荷 {payloadItemId} を選べなかった");
        }

        // 解放 API は解放しか持たないため、ロード経由で未解放へ戻す（前例 PlaceBlockProtocolTestSupport.LockBlock）
        // The controller only unlocks, so force the locked state through a state load (precedent: PlaceBlockProtocolTestSupport.LockBlock)
        public static void LockItem(ItemId itemId)
        {
            ServerContext.GetService<IGameUnlockStateDataController>().LoadUnlockState(new GameUnlockStateJsonObject
            {
                ItemUnlockStateInfos = new List<ItemUnlockStateInfoJsonObject>
                {
                    new() { ItemGuid = MasterHolder.ItemMaster.GetItemGuid(itemId).ToString(), IsUnlocked = false },
                },
            });
        }
    }
}
