using System.Collections.Generic;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Mooresmaster.Model.MachineRecipesModule;
using Game.Block.Interface.MachineUpgrade;

namespace Game.Block.Blocks.Machine.Inventory.Input
{
    /// <summary>
    ///     ブロック（ベルト・チェスト等）から機械への搬入口。自動搬入上限で積み、継承元のレベルで断った直近の1件を覚える
    ///     The inlet for inflow from blocks (belts, chests, ...); stacks under the auto-insert cap and remembers the latest refusal on the inheritance source level
    /// </summary>
    internal class MachineInputBlockInflow
    {
        // ベルトとチェストは断られた品を毎tick渡し直す。1tick前までの拒否を「今も届いて待っている」とみなし、照会がtickのどこで来ても同じ答えにする
        // Belts and chests re-offer a refused item every tick; a refusal up to one tick old counts as still waiting, whatever point of the tick the query comes at
        private const ulong RetryWindowTicks = 1;

        private readonly OpenableInventoryItemDataStoreService _itemDataStoreService;
        private readonly MachineInputSlotBinding _slotBinding;

        // 直近の拒否はレシピと現在条件も照合する
        // Validate the latest refusal against its recipe and current conditions
        private MachineInputLevelStall _latestRefusal;
        private ulong? _latestRefusalTick;
        private MachineRecipeMasterElement _refusalRecipe;
        private IItemStack _refusedStack;

        public MachineInputBlockInflow(OpenableInventoryItemDataStoreService itemDataStoreService, MachineInputSlotBinding slotBinding)
        {
            _itemDataStoreService = itemDataStoreService;
            _slotBinding = slotBinding;
        }

        public void ClearRefusal()
        {
            _latestRefusalTick = null;
            _refusalRecipe = null;
            _refusedStack = null;
        }

        public IItemStack Insert(IItemStack itemStack)
        {
            // 自動搬入上限で積む（旧 VanillaMachineInputInventory.InsertItemFromBlock の本体）
            // Stack under the auto-insert cap (the former body of VanillaMachineInputInventory.InsertItemFromBlock)
            var (slots, touchedSlots, remainders) = VanillaMachineInputSimulationUtil.SimulateAutoInsert(_itemDataStoreService.InventoryItems, new List<IItemStack> { itemStack }, _slotBinding);
            foreach (var slot in touchedSlots) _itemDataStoreService.SetItem(slot, slots[slot]);

            // 継承元のレベルで断った残りだけを、理由と時刻つきで覚える
            // Remember only a remainder refused on the source level, with its reason and tick
            var remainder = remainders[0];
            if (remainder.Count < itemStack.Count) _latestRefusalTick = null;
            // silent-ok: 束縛外の品・同じレベルの満杯は従来どおり残りを返すだけ / 品は搬入元（ベルト・チェスト）に残り、プレイヤーが見て気づく
            if (remainder.Count == 0 || !TryClassifyLevelRefusal(remainder, out var reason)) return remainder;
            _latestRefusal = new MachineInputLevelStall(reason, remainder.Id);
            _latestRefusalTick = GameUpdater.CurrentTick;
            _slotBinding.TryGetRecipe(out _refusalRecipe);
            _refusedStack = remainder;
            return remainder;
        }

        // ベルトの振り分け判定。実挿入と同じ上限で全量入るかだけを見て、拒否は覚えない（届いた品ではなく、流し先を探す問いのため）
        // Belt routing check; only whether everything fits under the real insert's cap, without remembering refusals (it asks where to route, not about an arrived item)
        public bool CanInsertAll(List<IItemStack> itemStacks)
        {
            foreach (var remainder in VanillaMachineInputSimulationUtil.SimulateAutoInsert(_itemDataStoreService.InventoryItems, itemStacks, _slotBinding).remainders)
            {
                if (remainder.Count != 0) return false;
            }
            return true;
        }

        // 直近の拒否が窓の内にあり、継承元スロットの数が選択レシピの継承元の要求数に足りないときだけ止まっている（加工状態は部品が見る）
        // Stalled only while the latest refusal is inside the window and the source slot holds fewer than the selected recipe requires (the component checks the process state)
        public bool TryGetStall(out MachineInputLevelStall stall)
        {
            stall = _latestRefusal;
            if (!_latestRefusalTick.HasValue || RetryWindowTicks < GameUpdater.CurrentTick - _latestRefusalTick.Value) return false;
            if (!_slotBinding.TryGetRecipe(out var recipe) || !MachineRecipeLevelInheritanceUtil.TryGetShape(recipe, out var shape)) return false;

            // silent-ok: レシピ変更や上限解放で古い拒否を捨てる / 最新の停止理由が照会結果に出る
            if (!ReferenceEquals(recipe, _refusalRecipe) || !TryClassifyLevelRefusal(_refusedStack, out var reason) || reason != _latestRefusal.Reason)
            {
                _latestRefusalTick = null;
                return false;
            }

            // 継承元が足りていて他の材料だけが欠けている間は、レベル違いを仕分けても動かないので理由にしない
            // While the source suffices and only other inputs are missing, sorting levels would not start it, so it is not the reason
            var sourceIndex = shape.SourceInputIndex;
            return _itemDataStoreService.InventoryItems[sourceIndex].Count < recipe.InputItems[sourceIndex].Count;
        }

        // 継承元の変種が、製作上限超えか、別レベルの入ったスロットのせいで断られたかを分ける
        // Tell whether a source variant was refused for being above the craft cap or for a slot holding another level
        private bool TryClassifyLevelRefusal(IItemStack remainder, out MachineInputLevelRefusalReason reason)
        {
            reason = default;
            if (!_slotBinding.TryGetRecipe(out var recipe) || !MachineRecipeLevelInheritanceUtil.TryGetShape(recipe, out var shape)) return false;
            if (!shape.ResolveSourceLevel(remainder.Id).HasValue) return false;

            // 上限超えは配置判定と同じ口で見分ける
            // Tell above-cap apart through the same entry as the placement check
            var sourceIndex = shape.SourceInputIndex;
            if (_slotBinding.CheckPlacement(sourceIndex, remainder) == MachineSlotPlacementCheck.LevelAboveCraftCap)
            {
                reason = MachineInputLevelRefusalReason.AboveCraftCap;
                return true;
            }

            // 別レベルが入っていて積めなかったときだけレベル違い。同じレベルの満杯は普通の詰まり
            // Mixed level only when another level occupies the slot; a full slot of the same level is an ordinary backup
            var slotItemId = _itemDataStoreService.InventoryItems[sourceIndex].Id;
            reason = MachineInputLevelRefusalReason.MixedLevel;
            return slotItemId != ItemMaster.EmptyItemId && slotItemId != remainder.Id;
        }
    }
}
