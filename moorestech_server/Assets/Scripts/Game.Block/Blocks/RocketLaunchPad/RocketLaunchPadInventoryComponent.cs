using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface.Component;
using Game.Context;
using static Game.Block.Interface.BlockException;

namespace Game.Block.Blocks.RocketLaunchPad
{
    // 発射台の専用スロットを公開し、搬入は割り当て先だけへ流す
    // Exposes bound launch-pad slots and routes insertion only to each assigned input
    public class RocketLaunchPadInventoryComponent : IOpenableBlockInventoryComponent, ISortExcludedSlots
    {
        private readonly RocketLaunchPadSlotLayout _layout;
        private readonly OpenableInventoryItemDataStoreService _slots;

        public RocketLaunchPadInventoryComponent(RocketLaunchPadSlotLayout layout, OpenableInventoryItemDataStoreService slots)
        {
            _layout = layout;
            _slots = slots;
        }

        public IReadOnlyList<IItemStack> InventoryItems
        {
            get
            {
                CheckDestroy(this);
                return _slots.InventoryItems;
            }
        }

        public IItemStack GetItem(int slot)
        {
            CheckDestroy(this);
            return _slots.GetItem(slot);
        }

        public void SetItem(int slot, IItemStack itemStack)
        {
            CheckDestroy(this);
            _slots.SetItem(slot, itemStack);
        }

        public void SetItem(int slot, ItemId itemId, int count)
        {
            CheckDestroy(this);
            _slots.SetItem(slot, itemId, count);
        }

        public IItemStack ReplaceItem(int slot, IItemStack itemStack)
        {
            CheckDestroy(this);
            return _slots.ReplaceItem(slot, itemStack);
        }

        public IItemStack ReplaceItem(int slot, ItemId itemId, int count)
        {
            CheckDestroy(this);
            return _slots.ReplaceItem(slot, itemId, count);
        }

        // 入れ口も出力口も束縛済みのスロットなので、どれも整理の対象にしない（機械のインベントリと同じ）
        // Every input and output slot is bound, so none takes part in sorting (same as machine inventories)
        public IReadOnlyCollection<int> SortExcludedSlots
        {
            get
            {
                CheckDestroy(this);
                return Enumerable.Range(0, GetSlotSize()).ToList();
            }
        }

        // 入れ口は割り当てたアイテムだけ、出力口は取り出し専用
        // Input slots accept only their assigned item; output slots are take-out only
        public bool IsAllowedToPlace(int slot, IItemStack itemStack)
        {
            CheckDestroy(this);
            if (itemStack.Count == 0) return true;
            // silent-ok: 配置可否の問い合わせはUIの移動サービスが拒否を扱う / プレイヤーは品が手元に残ることで気づく
            if (_layout.IsOutputSlot(slot)) return false;
            return _layout.RequirementAt(slot).ItemId == itemStack.Id;
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            CheckDestroy(this);
            return InsertIntoInputSlots(new List<IItemStack> { itemStack })[0];
        }

        public IItemStack InsertItem(IItemStack itemStack, InsertItemContext context)
        {
            CheckDestroy(this);
            return InsertItem(itemStack);
        }

        public IItemStack InsertItem(ItemId itemId, int count)
        {
            CheckDestroy(this);
            return InsertItem(ServerContext.ItemStackFactory.Create(itemId, count));
        }

        public List<IItemStack> InsertItem(List<IItemStack> itemStacks)
        {
            CheckDestroy(this);
            return InsertIntoInputSlots(itemStacks).Where(remainder => remainder.Count > 0).ToList();
        }

        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            CheckDestroy(this);
            return SimulateInsert(itemStacks).remainders.All(remainder => remainder.Count == 0);
        }

        private List<IItemStack> InsertIntoInputSlots(IReadOnlyList<IItemStack> itemStacks)
        {
            CheckDestroy(this);

            // 仮挿入の結果をそのまま書き込む。可否判定と実挿入で規則が食い違わないようにするため
            // Write the simulated result as-is so the check and the real insert never disagree
            var (slots, remainders) = SimulateInsert(itemStacks);
            foreach (var pair in slots) _slots.SetItem(pair.Key, pair.Value);
            return remainders;
        }

        // 書き込まずに、各スタックを自分の入れ口へ順に仮挿入する（前例 VanillaMachineInputSimulationUtil.SimulateInsert）
        // Simulates inserting each stack into its own input slot in order without writing (precedent: VanillaMachineInputSimulationUtil.SimulateInsert)
        private (Dictionary<int, IItemStack> slots, List<IItemStack> remainders) SimulateInsert(IReadOnlyList<IItemStack> itemStacks)
        {
            var simulated = new Dictionary<int, IItemStack>();
            var remainders = new List<IItemStack>(itemStacks.Count);
            foreach (var itemStack in itemStacks)
            {
                if (!_layout.TryGetInputSlot(itemStack.Id, out var slot))
                {
                    // silent-ok: 入れ口の無い品は余りとして呼び出し元へ返り、ベルト上や手元に残る / プレイヤーが入らないことを目視で気づく
                    remainders.Add(itemStack);
                    continue;
                }
                var result = (simulated.TryGetValue(slot, out var stack) ? stack : _slots.GetItem(slot)).AddItem(itemStack);
                simulated[slot] = result.ProcessResultItemStack;
                remainders.Add(result.RemainderItemStack);
            }
            return (simulated, remainders);
        }

        public int GetSlotSize()
        {
            CheckDestroy(this);
            return _slots.GetSlotSize();
        }

        public ReadOnlyCollection<IItemStack> CreateCopiedItems()
        {
            CheckDestroy(this);
            return _slots.CreateCopiedItems();
        }

        public bool IsDestroy { get; private set; }

        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
