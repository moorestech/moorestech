using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.BeltConveyor.Transport;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;

namespace Game.Block.Blocks.BeltConveyor
{
    // ベルコンblockが機械から受ける搬入口。押し込みはワールド全体の搬送の組へ進入距離1で渡し、1個入った分だけ減らして返す
    // The belt block's inlet for machines; a push goes to the world-wide transport assembly at entry length 1 and the stack returns reduced by the one item that entered
    // ベルコン上のアイテムはblockのスロットとして持たない。撤去で返さず、スロットは常に0
    // Items on the belt are not block slots; removal returns nothing and the slot count is always zero
    public class BeltConveyorInventoryComponent : IBlockInventory
    {
        private readonly BlockInstanceId _blockInstanceId;
        private readonly BeltTransportDatastore _beltTransportDatastore;

        public bool IsDestroy { get; private set; }

        public BeltConveyorInventoryComponent(BlockInstanceId blockInstanceId)
        {
            _blockInstanceId = blockInstanceId;
            _beltTransportDatastore = ServerContext.GetService<BeltTransportDatastore>();
        }

        public IItemStack InsertItem(IItemStack itemStack, InsertItemContext context)
        {
            BlockException.CheckDestroy(this);
            if (itemStack.Id == ItemMaster.EmptyItemId) return itemStack;
            var supplied = _beltTransportDatastore.Assembly.TrySupplyFromMachine(_blockInstanceId, context, itemStack.Id, itemStack.ItemInstanceId);
            return supplied ? itemStack.SubItem(1) : itemStack;
        }

        // 1回の押し込みで入るのは1個だけ。空きは接続ごとに搬入時に判定するので、ここでは個数だけを見る
        // Only one item enters per push; room is settled per connection at push time, so only the count is checked here
        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            BlockException.CheckDestroy(this);
            return itemStacks.Count == 1 && itemStacks[0].Count == 1;
        }

        public int GetSlotSize()
        {
            BlockException.CheckDestroy(this);
            return 0;
        }

        public IItemStack GetItem(int slot)
        {
            BlockException.CheckDestroy(this);
            return ServerContext.ItemStackFactory.CreatEmpty();
        }

        public void SetItem(int slot, IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);
        }

        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
