using System;
using System.Collections.Generic;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using MessagePack;
using UniRx;

namespace Game.Block.Blocks.Pump
{
    /// <summary>
    ///     ポンプの状態配信。生成中は毎tick、待機へ落ちた直後に1回発火する（採掘機 CheckStateAndInvokeEventUpdate と同じ節度）
    ///     Publishes pump state: every tick while generating and once on the drop to idle (same cadence as the miner's CheckStateAndInvokeEventUpdate)
    /// </summary>
    public class PumpStateComponent : IBlockStateObservable, IUpdatableBlockComponent
    {
        public IObservable<Unit> OnChangeBlockState => _onChangeBlockState;
        private readonly Subject<Unit> _onChangeBlockState = new();

        private readonly IReadOnlyList<FluidGenerationEntry> _entries;
        private readonly IPumpGenerationState _generationState;
        private bool _wasGenerating;

        public PumpStateComponent(IReadOnlyList<FluidGenerationEntry> entries, IPumpGenerationState generationState)
        {
            _entries = entries;
            _generationState = generationState;
        }

        public void Update()
        {
            BlockException.CheckDestroy(this);

            var isGenerating = _generationState.CanGenerateFluid;
            if (isGenerating || _wasGenerating) _onChangeBlockState.OnNext(Unit.Default);
            _wasGenerating = isGenerating;
        }

        public BlockStateDetail[] GetBlockStateDetails()
        {
            BlockException.CheckDestroy(this);

            var pumpingFluids = new List<PumpingFluidMessagePack>();
            foreach (var entry in _entries) pumpingFluids.Add(new PumpingFluidMessagePack(entry.FluidId, entry.PerSecond));

            var detail = new PumpBlockStateDetail(pumpingFluids);
            return new[] { new BlockStateDetail(PumpBlockStateDetail.BlockStateDetailKey, MessagePackSerializer.Serialize(detail)) };
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
            _onChangeBlockState.Dispose();
        }
    }

    /// <summary>
    ///     電気・歯車の両ポンプが「いま生成できるか」を同じ形で答える
    ///     Both the electric and gear pumps answer "can it generate now" through the same shape
    /// </summary>
    public interface IPumpGenerationState
    {
        bool CanGenerateFluid { get; }
    }
}
