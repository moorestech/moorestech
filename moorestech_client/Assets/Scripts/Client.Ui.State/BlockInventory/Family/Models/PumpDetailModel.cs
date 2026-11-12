using Client.Ui.State.BlockDetail;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.Diagnostics;
using Game.Block.Interface.State;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace Client.Ui.State.BlockInventory.Family.Models
{
    /// <summary>
    /// 追跡ブロックの状態詳細からポンプの詳細を組んで配る
    /// Builds and publishes the pump detail from the tracked block's state details
    /// </summary>
    public sealed class PumpDetailModel : IPumpDetailModel, IInitializable
    {
        private readonly OpenBlockDetailTracker _tracker;
        private readonly AnomalyReportGate _conversionAnomalyGate = new();
        private readonly ReactiveProperty<SectionContent<PumpDetail>> _current = new(SectionContent<PumpDetail>.Absent);

        public IReadOnlyReactiveProperty<SectionContent<PumpDetail>> Current => _current;

        public PumpDetailModel(OpenBlockDetailTracker tracker)
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
                _conversionAnomalyGate.Clear();
                _current.Value = SectionContent<PumpDetail>.Absent;
                return;
            }

            // silent-ok: ポンプの状態詳細が無いブロックはポンプでない / 区画が出ないことを並べ比べで見る
            var pump = block.GetStateDetail<PumpBlockStateDetail>(PumpBlockStateDetail.BlockStateDetailKey);
            if (pump == null)
            {
                _conversionAnomalyGate.Clear();
                _current.Value = SectionContent<PumpDetail>.Absent;
                return;
            }

            // 偽の理由は変換が異常として返すので、同じ間は 1 回だけ出す（異常なしの偽は変換側の silent-ok の静かな待ち）
            // The reason for false comes back as an anomaly, reported once while it persists (a false without anomaly is the conversion's silent-ok wait)
            var common = block.GetStateDetail<CommonMachineBlockStateDetail>(CommonMachineBlockStateDetail.BlockStateDetailKey);
            var reading = PumpDetailConversion.Build(pump, block.BlockMasterElement.BlockParam, common);
            var anomaly = reading.TryGetAnomaly(out var anomalyText) ? anomalyText : null;
            if (_conversionAnomalyGate.ShouldReport(anomaly)) Debug.LogError(anomaly);
            _current.Value = reading.TryGetDetail(out var detail) ? SectionContent<PumpDetail>.Present(detail) : SectionContent<PumpDetail>.Absent;
        }
    }
}
