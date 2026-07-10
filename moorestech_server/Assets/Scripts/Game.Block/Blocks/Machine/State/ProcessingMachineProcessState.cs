using System.Collections.Generic;
using Core.Inventory;
using Core.Item.Interface;
using Game.Block.Blocks.Util;
using Game.Block.Blocks.Machine.State.Util;
using Game.Fluid;

namespace Game.Block.Blocks.Machine.State
{
    // 加工ステート。電力に応じて進行し、完了で待機へ戻る
    // Processing state: advances with power and returns to idle on completion
    internal class ProcessingMachineProcessState : IMachineProcessState
    {
        
        public ProcessState State => ProcessState.Processing;
        private readonly MachineProcessContext _context;

        public uint TotalTicks { get; private set; }
        public uint RemainingTicks  { get; private set; }

        public IReadOnlyList<IItemStack> PendingOutputs => _pendingOutputs;
        public IReadOnlyList<FluidStack> PendingFluidOutputs => _pendingFluidOutputs;
        public IReadOnlyList<IItemStack> ConsumedItems => _consumedItems;
        public bool HasProcessing => _consumedItems != null;
        private List<IItemStack> _pendingOutputs;
        private List<FluidStack> _pendingFluidOutputs;
        private List<IItemStack> _consumedItems;
        
        // 完了直前に産出リストを差し替えるフック（清浄室のチップ抽選など、OnExit挿入前の置き換え用）
        // Hook to replace the pending output list just before completion (e.g. clean-room chip draw swaps items before OnExit inserts them)
        public void ReplacePendingOutputs(List<IItemStack> outputs)
        {
            _pendingOutputs = outputs;
        }
        
        public ProcessingMachineProcessState(
            MachineProcessContext context,
            uint totalTicks,
            uint remainingTicks,
            List<IItemStack> pendingOutputs,
            List<FluidStack> pendingFluidOutputs,
            List<IItemStack> consumedItems)
        {
            _context = context;
            TotalTicks = totalTicks;
            RemainingTicks = remainingTicks;
            _pendingOutputs = pendingOutputs;
            _pendingFluidOutputs = pendingFluidOutputs;
            _consumedItems = consumedItems;
        }

        // 選択レシピから確定した加工スナップショットを設定する
        // Set the processing snapshot realized from the selected recipe
        public void SetProcessing(uint totalTicks, List<IItemStack> pendingOutputs, List<FluidStack> pendingFluidOutputs, List<IItemStack> consumedItems)
        {
            TotalTicks = totalTicks;
            RemainingTicks = totalTicks;
            _pendingOutputs = pendingOutputs;
            _pendingFluidOutputs = pendingFluidOutputs;
            _consumedItems = consumedItems;
        }

        public void OnEnter() { }

        public ProcessState GetNextUpdate()
        {
            // 電力、モジュールに基づいてこのティックで引くティック数を計算
            // Calculate the number of ticks to consume this tick based on power and modules
            var effectiveRequestPower = _context.RequestPower * _context.EffectComponent.AggregateCurrent().PowerMultiplier;
            var subTicks = MachineCurrentPowerToSubSecond.GetSubTicks(_context.CurrentPower, effectiveRequestPower);

            // 残りtickを使い切ったら完了して待機へ
            // Once remaining ticks are exhausted, finish and return to idle
            if (subTicks >= RemainingTicks)
            {
                RemainingTicks = 0;
                return ProcessState.Idle;
            }

            RemainingTicks -= subTicks;
            return ProcessState.Processing;
        }

        // 完了時だけ確定済み産出物を払い出す
        // Emit the realized outputs only on normal completion
        public void OnExit()
        {
            _context.OutputInventory.InsertOutputSlot(_pendingOutputs, _pendingFluidOutputs);
            ClearProcessing();
        }

        public bool TryCancel(IOpenableInventory playerMainInventory)
        {
            // アイテムを全量返却できるときだけ加工を破棄する
            // Discard processing only when every consumed item can be refunded
            if (!_context.InputInventory.TryRefundConsumedItems(_consumedItems, playerMainInventory)) return false;
            ClearProcessing();
            return true;
        }

        private void ClearProcessing()
        {
            // 完了済み加工の再返却・再保存を防ぐため全情報を消去する
            // Clear all data to prevent a completed job from being refunded or saved again
            _pendingOutputs = null;
            _pendingFluidOutputs = null;
            _consumedItems = null;
            TotalTicks = 0;
            RemainingTicks = 0;
        }
    }
}
