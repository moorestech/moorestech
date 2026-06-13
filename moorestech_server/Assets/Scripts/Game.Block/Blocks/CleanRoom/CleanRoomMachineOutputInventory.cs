using System;
using System.Collections.Generic;
using System.Linq;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Service;
using Game.Block.Component;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Event;
using Game.Block.Blocks.Machine.Inventory;
using Game.Context;
using Game.Fluid;
using Mooresmaster.Model.MachineRecipesModule;
using UniRx;

namespace Game.Block.Blocks.CleanRoom
{
    // VanillaMachineOutputInventory をコピーして専用化。レベル付き出力は EUV→天井→down-bin 抽選で差し替え、空スロット予約で消失を防ぐ。
    // Copied from VanillaMachineOutputInventory; leveled outputs are drawn (EUV/ceiling/down-bin) and reserved by empty-slot count.
    public class CleanRoomMachineOutputInventory : IVanillaMachineSubInventory
    {
        public IReadOnlyList<IItemStack> OutputSlot => _itemDataStoreService.InventoryItems;
        IReadOnlyList<IItemStack> IVanillaMachineSubInventory.Items => OutputSlot;
        public IReadOnlyList<FluidContainer> FluidOutputSlot => _fluidContainers;

        private readonly BlockOpenableInventoryUpdateEvent _blockInventoryUpdate;
        private readonly ConnectingInventoryListPriorityInsertItemService _connectInventoryService;
        private readonly BlockInstanceId _blockInstanceId;
        private readonly CleanRoomStateReceiverComponent _receiver;

        // EUV失敗/Out で出力なしになったときに呼ぶテスト可視コールバック（サイレント消失アサート用）
        // Invoked when an EUV fail / Out yields no output (feeds the no-silent-loss assertion)
        private readonly Action _onNoOutputForTest;

        private readonly int _inputSlotSize;
        private readonly OpenableInventoryItemDataStoreService _itemDataStoreService;
        private readonly FluidContainer[] _fluidContainers;

        public CleanRoomMachineOutputInventory(int outputSlot, int outputTankCount, float innerTankCapacity, IItemStackFactory itemStackFactory,
            BlockOpenableInventoryUpdateEvent blockInventoryUpdate, BlockInstanceId blockInstanceId, int inputSlotSize, BlockConnectorComponent<IBlockInventory> blockConnectorComponent,
            CleanRoomStateReceiverComponent receiver, Action onNoOutputForTest)
        {
            _blockInventoryUpdate = blockInventoryUpdate;
            _blockInstanceId = blockInstanceId;
            _inputSlotSize = inputSlotSize;
            _receiver = receiver;
            _onNoOutputForTest = onNoOutputForTest;
            _itemDataStoreService = new OpenableInventoryItemDataStoreService(InvokeEvent, itemStackFactory, outputSlot);
            _connectInventoryService = new ConnectingInventoryListPriorityInsertItemService(blockInstanceId, blockConnectorComponent);

            _fluidContainers = new FluidContainer[outputTankCount];
            for (var i = 0; i < outputTankCount; i++)
            {
                _fluidContainers[i] = new FluidContainer(innerTankCapacity);
            }

            GameUpdater.UpdateObservable.Subscribe(_ => Update());
        }

        private void Update()
        {
            InsertConnectInventory();
        }

        // レベル付き出力は完了時 ItemId が down-bin/効果で変わり得るため空スロット方式で予約する
        // Leveled outputs reserve EMPTY slots because the final ItemId can change via down-bin / effect
        public bool IsAllowedToOutputItem(MachineRecipeMasterElement machineRecipe)
        {
            var leveledCount = 0;
            foreach (var itemOutput in machineRecipe.OutputItems)
            {
                if (MasterHolder.SemiconductorChipMaster.TryGetDistribution(machineRecipe.MachineRecipeGuid, itemOutput.ItemGuid, out _))
                {
                    leveledCount++;
                    continue;
                }

                // 非レベル出力は Vanilla と同じ判定（既存スタックへの追記も可）
                // Non-leveled outputs use the vanilla check (may append to an existing stack)
                var outputItemId = MasterHolder.ItemMaster.GetItemId(itemOutput.ItemGuid);
                var outputItemStack = ServerContext.ItemStackFactory.Create(outputItemId, itemOutput.Count);
                var isAllowed = OutputSlot.Aggregate(false, (current, slot) => slot.IsAllowedToAdd(outputItemStack) || current);
                if (!isAllowed) return false;
            }

            // 空スロット数 ≥ レベル付き出力要素数 を要求（MaxGrade 非依存）
            // Require empty slots >= leveled output count (independent of the current MaxGrade)
            if (leveledCount > 0)
            {
                var emptySlots = OutputSlot.Count(slot => slot.Id == ItemMaster.EmptyItemId);
                if (emptySlots < leveledCount) return false;
            }

            // 液体出力のスペースを確認する（Vanilla のコピー）
            // Check fluid output space (vanilla copy)
            for (var i = 0; i < machineRecipe.OutputFluids.Length; i++)
            {
                if (i >= _fluidContainers.Length) return false;

                var outputFluidId = MasterHolder.FluidMaster.GetFluidId(machineRecipe.OutputFluids[i].FluidGuid);
                var amount = machineRecipe.OutputFluids[i].Amount;

                if (_fluidContainers[i].FluidId != FluidMaster.EmptyFluidId && _fluidContainers[i].FluidId != outputFluidId) return false;
                if (_fluidContainers[i].Capacity - _fluidContainers[i].Amount < amount) return false;
            }

            return true;
        }

        // 出力要素単位で抽選して格納する（cycleSeed で決定的）
        // Emit outputs per element, drawing leveled ones deterministically from cycleSeed
        public void InsertOutputSlot(MachineRecipeMasterElement machineRecipe, long cycleSeed)
        {
            var effect = _receiver.CurrentEffect;

            for (var outputIndex = 0; outputIndex < machineRecipe.OutputItems.Length; outputIndex++)
            {
                var itemOutput = machineRecipe.OutputItems[outputIndex];

                ItemId outputItemId;
                var isLeveled = MasterHolder.SemiconductorChipMaster.TryGetDistribution(
                    machineRecipe.MachineRecipeGuid, itemOutput.ItemGuid, out _);
                if (isLeveled)
                {
                    // percent は EUV catastrophic 失敗率の補数（=成功率）。スキーマ default=1。
                    // percent is the complement of the EUV catastrophic failure rate (= success rate). schema default 1.
                    var percent = itemOutput.Percent;
                    var draw = SemiconductorChipDraw.TryResolveOutputItemId(
                        machineRecipe.MachineRecipeGuid, itemOutput.ItemGuid,
                        effect.MaxGrade, effect.DownBinRate, percent,
                        cycleSeed, outputIndex, out outputItemId);

                    if (draw != DrawResult.Drawn)
                    {
                        // EUV失敗/Out：出力なし（サイレント Lv1 禁止）。テストカウンタへ通知して何も出さない
                        // EUV fail / Out: no output (no silent Lv1). Notify the test counter and emit nothing
                        _onNoOutputForTest?.Invoke();
                        continue;
                    }
                }
                else
                {
                    // 副産物（分布なし）はベース ItemId のまま
                    // By-products keep their base ItemId
                    outputItemId = MasterHolder.ItemMaster.GetItemId(itemOutput.ItemGuid);
                }

                InsertToSlot(ServerContext.ItemStackFactory.Create(outputItemId, itemOutput.Count));
            }

            // 液体出力（Vanilla のコピー）
            // Fluid outputs (vanilla copy)
            for (var i = 0; i < machineRecipe.OutputFluids.Length; i++)
            {
                if (i >= _fluidContainers.Length) break;

                var fluidId = MasterHolder.FluidMaster.GetFluidId(machineRecipe.OutputFluids[i].FluidGuid);
                _fluidContainers[i].AddLiquid(new FluidStack(machineRecipe.OutputFluids[i].Amount, fluidId), FluidContainer.Empty);
            }
        }

        // 同 ItemId スロット→空スロットの順に1スタックを格納する
        // Insert a single stack into a matching-Id slot first, then an empty one
        private void InsertToSlot(IItemStack outputItemStack)
        {
            for (var i = 0; i < OutputSlot.Count; i++)
            {
                if (!OutputSlot[i].IsAllowedToAdd(outputItemStack)) continue;

                var item = OutputSlot[i].AddItem(outputItemStack).ProcessResultItemStack;
                _itemDataStoreService.SetItem(i, item);
                break;
            }
        }

        private void InsertConnectInventory()
        {
            for (var i = 0; i < OutputSlot.Count; i++)
                _itemDataStoreService.SetItem(i, _connectInventoryService.InsertItem(OutputSlot[i]));
        }

        public void SetItem(int slot, IItemStack itemStack)
        {
            _itemDataStoreService.SetItem(slot, itemStack);
        }

        public void SetItemWithoutEvent(int slot, IItemStack itemStack)
        {
            _itemDataStoreService.SetItemWithoutEvent(slot, itemStack);
        }

        private void InvokeEvent(int slot, IItemStack itemStack)
        {
            _blockInventoryUpdate.OnInventoryUpdateInvoke(new BlockOpenableInventoryUpdateEventProperties(
                _blockInstanceId, slot + _inputSlotSize, itemStack));
        }
    }
}
