using Client.Ui.State.BlockDetail;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.Diagnostics;
using Game.Block.Interface.State;
using Mooresmaster.Model.BlocksModule;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace Client.Ui.State.BlockInventory.Family.Models
{
    /// <summary>
    /// 追跡ブロックの状態詳細から採掘機の詳細を組んで配る
    /// Builds and publishes the miner detail from the tracked block's state details
    /// </summary>
    public sealed class MinerDetailModel : IMinerDetailModel, IInitializable
    {
        private readonly OpenBlockDetailTracker _tracker;
        private readonly AnomalyReportGate _gearAnomalyGate = new();
        private readonly ReactiveProperty<SectionContent<MinerDetail>> _current = new(SectionContent<MinerDetail>.Absent);

        public IReadOnlyReactiveProperty<SectionContent<MinerDetail>> Current => _current;

        public MinerDetailModel(OpenBlockDetailTracker tracker)
        {
            _tracker = tracker;
        }

        public void Initialize()
        {
            _tracker.OnTrackedBlockChanged.Subscribe(_ => Rebuild());
            _tracker.OnTrackedBlockStateChanged.Subscribe(_ => Rebuild());
            Rebuild();
        }

        private void Rebuild()
        {
            var block = _tracker.TrackedBlock;
            // silent-ok: 追跡ブロックが無いとき区画は無い（閉と同じ） / 追跡替えの購読で直ちに組み直す
            if (block == null)
            {
                _gearAnomalyGate.ShouldReport(null);
                _current.Value = SectionContent<MinerDetail>.Absent;
                return;
            }

            // 3 つが揃うときだけ出す
            // Show only when the miner detail, common and IMinerParam are all present, as the Web host guard
            var miner = block.GetStateDetail<CommonMinerBlockStateDetail>(CommonMinerBlockStateDetail.BlockStateDetailKey);
            var common = block.GetStateDetail<CommonMachineBlockStateDetail>(CommonMachineBlockStateDetail.BlockStateDetailKey);
            var param = block.BlockMasterElement.BlockParam;
            // silent-ok: 採掘機でない・状態詳細が揃う前は区画が無い（Web ホストと同じ） / 状態詳細の更新で組み直す
            if (miner == null || common == null || param is not IMinerParam minerParam)
            {
                _gearAnomalyGate.ShouldReport(null);
                _current.Value = SectionContent<MinerDetail>.Absent;
                return;
            }
            // 歯車駆動の異常は、同じ間は 1 回だけ出す
            // Report a gear-drive anomaly once while it persists
            var gear = GearDriveConversion.Read(block, param);
            if (_gearAnomalyGate.ShouldReport(gear.Anomaly)) Debug.LogError(gear.Anomaly);
            _current.Value = SectionContent<MinerDetail>.Present(FamilyDetailConversion.Miner(miner, common, minerParam, gear.IsGearDriven));
        }
    }
}
