using Client.Game.InGame.UI.UIState.State;
using Client.Ui.State.BlockDetail;
using Client.Ui.State.BlockInventory.Fluid;
using Client.Ui.State.BlockInventory.Logic.Panel;
using Client.Ui.State.BlockInventory.Panel;
using Game.Block.Blocks.TrainRail;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.Train.Platform
{
    /// <summary>
    /// 開いている駅・プラットフォームの描画値を、パネルの状態・追跡ブロック・液体の Model から配る
    /// Publishes the open station or platform's draw values from the panel state, the tracked block and the fluid model
    /// </summary>
    public sealed class TrainPlatformBodyModel : ITrainPlatformBodySource, IInitializable
    {
        private readonly IBlockInventoryPanelSource _panels;
        private readonly OpenBlockDetailTracker _tracker;
        private readonly IBlockFluidSlotsModel _fluidSlots;
        private readonly ReactiveProperty<TrainPlatformBodyState> _current = new(TrainPlatformBodyState.Closed);

        public IReadOnlyReactiveProperty<TrainPlatformBodyState> Current => _current;

        public TrainPlatformBodyModel(IBlockInventoryPanelSource panels, OpenBlockDetailTracker tracker, IBlockFluidSlotsModel fluidSlots)
        {
            _panels = panels;
            _tracker = tracker;
            _fluidSlots = fluidSlots;
        }

        public void Initialize()
        {
            // パネルと液体の Model のどちらの通知でも組み直す（追跡替えの直後は 1 回目が前の液体で組まれ、同じフレームで液体の配り直しが直す。D-35）
            // Either the panel or the fluid model notification rebuilds (right after a tracking change the first build uses the previous fluid and the fluid republish fixes it in the same frame; D-35)
            _panels.Current.Subscribe(_ => Rebuild());
            _fluidSlots.Current.Subscribe(_ => Rebuild());
        }

        private void Rebuild()
        {
            // 本文の種類はパネルの登録表（BlockComponentRegistry）で決まり、ここで blockType を並べない
            // The body kind comes from the panel registry (BlockComponentRegistry); no blockType list here
            if (_panels.Current.Value is not OpenBlockInventoryPanel { Error: BlockInventoryPanelError.None } open || open.Components.Body != BlockInventoryBodyKind.TrainPlatform)
            {
                _current.Value = TrainPlatformBodyState.Closed;
                return;
            }
            var block = _tracker.TrackedBlock;
            // silent-ok: 追跡の替わりがパネルより後に届く一瞬だけ閉じた値 / 直後の追跡替えで液体の Model が配り直すので組み直し（D-35）、出ないままなら L1・録画で本文が空のまま見える
            if (block == null)
            {
                _current.Value = TrainPlatformBodyState.Closed;
                return;
            }

            // StateDetail はサーバー由来の外部データなので無いことがある（Reader が扱う）
            // StateDetails come from the server and may be missing; the Reader handles that
            var stationName = block.GetStateDetail<TrainStationNameStateDetail>(TrainStationNameStateDetail.BlockStateDetailKey);
            var transfer = block.GetStateDetail<TrainPlatformTransferStateDetail>(TrainPlatformTransferStateDetail.BlockStateDetailKey);
            _current.Value = TrainPlatformDetailReader.Read(open.BlockType, block.BlockMasterElement.BlockParam, stationName, transfer, open.ItemSlots, _fluidSlots.Current.Value.Slots);
        }
    }
}
