using Game.Block.Blocks.Util;

namespace Game.Block.Blocks.Machine.State
{
    // 加工ステート。電力に応じて進行し、完了で待機へ戻る
    // Processing state: advances with power and returns to idle on completion
    internal class ProcessingMachineProcessState : IMachineProcessState
    {
        private readonly MachineProcessContext _context;

        public ProcessingMachineProcessState(MachineProcessContext context)
        {
            _context = context;
        }

        public ProcessState State => ProcessState.Processing;

        // 開始時に入力を消費し残りtickを設定する
        // Consume inputs and set remaining ticks on start
        public void OnEnter()
        {
            _context.InputInventory.ReduceInputSlot(_context.ProcessingRecipe);
            _context.RemainingTicks = _context.ProcessingRecipeTicks;
        }

        public ProcessState GetNextUpdate()
        {
            var subTicks = MachineCurrentPowerToSubSecond.GetSubTicks(_context.CurrentPower, _context.EffectiveRequestPower);

            // 電力を消費する
            // Consume power
            _context.UsedPower = true;

            // 残りtickを使い切ったら完了して待機へ
            // Once remaining ticks are exhausted, finish and return to idle
            if (subTicks >= _context.RemainingTicks)
            {
                _context.RemainingTicks = 0;
                return ProcessState.Idle;
            }

            _context.RemainingTicks -= subTicks;
            return ProcessState.Processing;
        }

        // 完了時に産出物を払い出す（旧セーブは産出予定が無いため再抽選）
        // Output the produced items on completion (re-roll for old saves that lack pending outputs)
        public void OnExit()
        {
            var outputs = _context.PendingOutputs ?? _context.CreateRealizedOutputs(_context.ProcessingRecipe, _context.EffectComponent.AggregateCurrent());
            _context.OutputInventory.InsertOutputSlot(outputs, _context.CreateFluidOutputs(_context.ProcessingRecipe));
            _context.PendingOutputs = null;
        }
    }
}
