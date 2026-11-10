using Client.Ui.State.BlockDetail;
using Client.Ui.State.BlockInventory.Common;
using Game.Block.Interface.State;
using UniRx;
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
                _current.Value = SectionContent<PumpDetail>.Absent;
                return;
            }

            // silent-ok: ポンプの状態詳細が無いブロックはポンプでない / 区画が出ないことを並べ比べで見る
            var pump = block.GetStateDetail<PumpBlockStateDetail>(PumpBlockStateDetail.BlockStateDetailKey);
            if (pump == null)
            {
                _current.Value = SectionContent<PumpDetail>.Absent;
                return;
            }

            // 偽なら変換側が理由をログへ出す
            // When the conversion returns false it has already logged why (the quiet wait carries its own silent-ok)
            var common = block.GetStateDetail<CommonMachineBlockStateDetail>(CommonMachineBlockStateDetail.BlockStateDetailKey);
            var built = PumpDetailConversion.TryBuild(pump, block.BlockMasterElement.BlockParam, common, out var detail);
            _current.Value = built ? SectionContent<PumpDetail>.Present(detail) : SectionContent<PumpDetail>.Absent;
        }
    }
}
