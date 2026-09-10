using System.Collections.Generic;
using System.Linq;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.Service;
using Game.Block.Component;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Event;
using Game.Context;
using Game.Fluid;
using Mooresmaster.Model.MachineRecipesModule;
using UniRx;
using Game.Block.Interface.Component.ConnectJudge;

namespace Game.Block.Blocks.Machine.Inventory
{
    public class VanillaMachineOutputInventory : IVanillaMachineSubInventory
    {
        public IReadOnlyList<IItemStack> OutputSlot => _itemDataStoreService.InventoryItems;
        IReadOnlyList<IItemStack> IVanillaMachineSubInventory.Items => OutputSlot;
        public IReadOnlyList<FluidContainer> FluidOutputSlot => _fluidContainers;
        
        private readonly BlockOpenableInventoryUpdateEvent _blockInventoryUpdate;
        private readonly ConnectingInventoryListPriorityInsertItemService _connectInventoryService;
        private readonly BlockInstanceId _blockInstanceId;
        
        private readonly int _inputSlotSize;
        private readonly OpenableInventoryItemDataStoreService _itemDataStoreService;
        private readonly FluidContainer[] _fluidContainers;

        // 選択レシピへのスロット束縛判定。スロットjは生産物jのレベルファミリー枠（2026-08-30裁定）
        // Slot-binding decision against the selected recipe; slot j is output j's level-family frame (ruling 2026-08-30)
        private readonly MachineOutputSlotBinding _slotBinding = new();

        public VanillaMachineOutputInventory(int outputSlot, int outputTankCount, float innerTankCapacity, IItemStackFactory itemStackFactory,
            BlockOpenableInventoryUpdateEvent blockInventoryUpdate, BlockInstanceId blockInstanceId, int inputSlotSize, BlockConnectorComponent<IBlockInventory, DefaultConnectJudge> blockConnectorComponent)
        {
            _blockInventoryUpdate = blockInventoryUpdate;
            _blockInstanceId = blockInstanceId;
            _inputSlotSize = inputSlotSize;
            _itemDataStoreService = new OpenableInventoryItemDataStoreService(InvokeEvent, itemStackFactory, outputSlot);
            _connectInventoryService = new ConnectingInventoryListPriorityInsertItemService(blockInstanceId, blockConnectorComponent);
            
            _fluidContainers = new FluidContainer[outputTankCount];
            for (var i = 0; i < outputTankCount; i++)
            {
                _fluidContainers[i] = new FluidContainer(innerTankCapacity);
            }
        }

        // 出力スロットごとの許可アイテム集合を束縛する。品目数が物理スロット数を超えないことはマスタ検証が保証する
        // Bind the allowed-item set per output slot; master validation guarantees the item count never exceeds the physical slots
        internal void SetBoundOutputs(IReadOnlyList<IReadOnlyCollection<ItemId>> allowedItemsPerSlot)
        {
            _slotBinding.SetBoundOutputs(allowedItemsPerSlot);
        }

        // 出力スロットjは生産物jのレベルファミリーだけ置ける。プレイヤー操作の可否判定
        // Output slot j accepts only output j's level family; used for player-placement checks
        public bool IsAllowedToPlace(int localSlot, IItemStack itemStack)
        {
            return _slotBinding.IsAllowedToPlace(localSlot, itemStack);
        }

        /// <summary>
        ///     産出スタック列を格納できるか仮想挿入で判定する
        ///     Check via virtual insertion whether the output stacks fit
        /// </summary>
        public bool CanStoreOutputs(IReadOnlyList<IItemStack> itemOutputs, IReadOnlyList<FluidStack> fluidOutputs)
        {
            return TrySimulateOutputs(itemOutputs, fluidOutputs, out _);
        }

        /// <summary>
        ///     アイテム出力と液体出力を格納する
        ///     Insert the item and fluid outputs
        /// </summary>
        public void InsertOutputSlot(IReadOnlyList<IItemStack> itemOutputs, IReadOnlyList<FluidStack> fluidOutputs)
        {
            // 仮判定と実挿入で同じシミュレータを通すので、通過した結果をそのままコミットするだけでよい
            // The check and the real insert share one simulator, so a passing result is simply committed
            if (!TrySimulateOutputs(itemOutputs, fluidOutputs, out var simulatedSlots))
            {
                UnityEngine.Debug.LogError("出力確定直前のCanStoreOutputsを通過したのに実挿入で受け入れ不可判定になり生産物が消失した");
                return;
            }

            for (var slot = 0; slot < simulatedSlots.Count; slot++)
            {
                // 積まれなかったスロットは同じインスタンスのままなので、更新イベントを出さない
                // A slot nothing landed in keeps the same instance, so it emits no update event
                if (ReferenceEquals(simulatedSlots[slot], OutputSlot[slot])) continue;
                _itemDataStoreService.SetItem(slot, simulatedSlots[slot]);
            }

            for (var i = 0; i < fluidOutputs.Count; i++)
            {
                _fluidContainers[i].AddLiquid(fluidOutputs[i]);
            }
        }

        // アイテムと液体を出力先の複製へ積む唯一のシミュレータ。仮判定と実挿入がこれを共有するため両者は乖離しない
        // The single simulator that stacks items and fluids into a copy of the outputs; the check and the real insert share it and can never diverge
        private bool TrySimulateOutputs(IReadOnlyList<IItemStack> itemOutputs, IReadOnlyList<FluidStack> fluidOutputs, out List<IItemStack> simulatedSlots)
        {
            simulatedSlots = OutputSlot.ToList();
            if (!IsFluidOutputAllowed()) return false;

            // 実現出力kは出力スロット(k % 生産物数)へ固定で積む
            // Realized output k always lands in output slot (k % output count)
            for (var k = 0; k < itemOutputs.Count; k++)
            {
                var slot = _slotBinding.ResolveSlot(k);
                if (!simulatedSlots[slot].IsAllowedToAdd(itemOutputs[k])) return false;

                var result = simulatedSlots[slot].AddItem(itemOutputs[k]);
                if (result.RemainderItemStack.Count != 0) return false;
                simulatedSlots[slot] = result.ProcessResultItemStack;
            }

            return true;

            #region Internal

            bool IsFluidOutputAllowed()
            {
                // 液体の出力スペースをチェック
                // Check output space for fluids
                for (var i = 0; i < fluidOutputs.Count; i++)
                {
                    var outputFluid = fluidOutputs[i];

                    // 既に異なる液体が入っている場合、または容量が不足している場合
                    // If a different fluid is already present, or the remaining capacity is insufficient
                    if (_fluidContainers[i].FluidId != FluidMaster.EmptyFluidId && _fluidContainers[i].FluidId != outputFluid.FluidId)
                    {
                        return false;
                    }

                    if (_fluidContainers[i].Capacity - _fluidContainers[i].Amount < outputFluid.Amount)
                    {
                        return false;
                    }
                }

                return true;
            }

            #endregion
        }
        
        // 産出スロットを接続先インベントリへ払い出す。駆動はプロセッサコンポーネントのUpdate（自前購読は持たない）
        // Push output slots into connected inventories; driven by the processor component's Update (no self subscription)
        public void InsertConnectInventory()
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