using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Blocks.SpaceElevator
{
    // 衛星の入口が受け付けるアイテムの定義。サーバーの配置可否・搬入確認と、表示側の受け付け情報が同じ定義を使う
    // Definition of the items the satellite inlet accepts; the server's placement/insertion checks and the display's accepted items share it
    public class SpaceElevatorCarriedPayloads
    {
        private readonly HashSet<ItemId> _itemIds;

        public SpaceElevatorCarriedPayloads(SpaceElevatorBaseBlockParam param)
        {
            _itemIds = new HashSet<ItemId>(ItemIds(param));
        }

        public static IReadOnlyList<ItemId> ItemIds(SpaceElevatorBaseBlockParam param)
        {
            return param.CarriedPayloadItemGuids.Select(guid => MasterHolder.ItemMaster.GetItemId(guid)).ToList();
        }

        public bool Contains(ItemId itemId)
        {
            return _itemIds.Contains(itemId);
        }

        // 全部が運ぶ積荷で、入口の1スタックへ余りなく収まるかを在庫を変えずに確かめる
        // Checks without mutating that every stack is a carried payload and fits into the single inlet stack with no remainder
        public bool CanAcceptAll(IItemStack inletStack, List<IItemStack> itemStacks)
        {
            var slot = inletStack;
            foreach (var itemStack in itemStacks)
            {
                // silent-ok: 搬送の事前確認は在庫を変えない / 呼び出し側が返値で詰まりを認識する
                if (!Contains(itemStack.Id)) return false;
                var result = slot.AddItem(itemStack);
                // silent-ok: 満杯の通常拒否 / 呼び出し側と入口UIに残数が届く
                if (0 < result.RemainderItemStack.Count) return false;
                slot = result.ProcessResultItemStack;
            }
            return true;
        }
    }
}
