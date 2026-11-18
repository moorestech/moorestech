using Client.Ui.State.BlockDetail;
using Client.Ui.State.BlockInventory.Common;
using Game.Block.Blocks.PowerGenerator;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.BlockInventory.Family.Models
{
    /// <summary>
    /// 追跡ブロックの状態詳細から発電機の詳細を組んで配る
    /// Builds and publishes the generator detail from the tracked block's state details
    /// </summary>
    public sealed class GeneratorDetailModel : IGeneratorDetailModel, IInitializable
    {
        private readonly OpenBlockDetailTracker _tracker;
        private readonly ReactiveProperty<SectionContent<GeneratorDetail>> _current = new(SectionContent<GeneratorDetail>.Absent);

        public IReadOnlyReactiveProperty<SectionContent<GeneratorDetail>> Current => _current;

        public GeneratorDetailModel(OpenBlockDetailTracker tracker)
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
                _current.Value = SectionContent<GeneratorDetail>.Absent;
                return;
            }

            // silent-ok: 発電機の状態詳細が無いブロックは発電機でない / 状態変化の購読で直ちに組み直す
            var generator = block.GetStateDetail<PowerGeneratorStateDetail>(PowerGeneratorStateDetail.StateDetailKey);
            _current.Value = generator == null ? SectionContent<GeneratorDetail>.Absent : SectionContent<GeneratorDetail>.Present(FamilyDetailConversion.Generator(generator));
        }
    }
}
