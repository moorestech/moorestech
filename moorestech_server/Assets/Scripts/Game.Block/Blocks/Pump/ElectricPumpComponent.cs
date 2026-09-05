using Game.Block.Blocks.Machine;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Game.EnergySystem;
using MessagePack;

namespace Game.Block.Blocks.Pump
{
    /// <summary>
    /// 所属セグメントの確定済み供給率から実効電力を導出してポンプProcessorへ渡す
    /// Derives effective power from its segment's settled supply rate and feeds the pump processor
    /// - UI: CommonMachineBlockStateDetailで配信（ADR 0010/0051）
    /// - UI: published via CommonMachineBlockStateDetail (ADR 0010/0051)
    /// </summary>
    public class ElectricPumpComponent : IElectricConsumer, IElectricTickPostHandler, IBlockStateDetail
    {
        public BlockInstanceId BlockInstanceId { get; }
        public ElectricPower RequestEnergy => new(_processor.EffectiveRequestPower);

        private readonly ElectricPumpProcessorComponent _processor;

        public ElectricPumpComponent(BlockInstanceId blockInstanceId, ElectricPumpProcessorComponent processor)
        {
            BlockInstanceId = blockInstanceId;
            _processor = processor;
        }

        public void OnElectricTickPostProcess(ElectricNetworkStatistics statistics)
        {
            BlockException.CheckDestroy(this);

            // 確定した供給率から実効電力を一度だけProcessorへ渡す
            // Push effective power to the processor once from the settled supply rate
            _processor.SupplyExternalPower(new ElectricPower(RequestEnergy.AsPrimitive() * statistics.PowerRate));
        }

        public BlockStateDetail[] GetBlockStateDetails()
        {
            BlockException.CheckDestroy(this);

            // 稼働状態は「汲み上げ対象あり ∧ タンクに空きあり」の2値。停止中は無い
            // The state is binary, generating or idle; there is no halted state
            var stateType = _processor.CanGenerateFluid ? VanillaMachineBlockStateConst.ProcessingState : VanillaMachineBlockStateConst.IdleState;

            // 電力はProcessorが同一tick時点で確定させた分子分母をそのまま配信する
            // Publish the numerator and denominator the processor latched at the same tick point
            var detail = new CommonMachineBlockStateDetail(_processor.CurrentPower, _processor.PublishedRequestPower, 0f, stateType, stateType);
            return new[] { new BlockStateDetail(CommonMachineBlockStateDetail.BlockStateDetailKey, MessagePackSerializer.Serialize(detail)) };
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
