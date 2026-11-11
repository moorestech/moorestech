using System.Collections.Generic;
using Client.Game.InGame.UI.UIState.State;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.BlockInventory.Panel;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.Train.Inventory
{
    /// <summary>
    /// 開いている貨車の中身を配る。スロットはパネルの状態、液体はサブインベントリから
    /// Publishes the open car's contents: slots from the panel state, fluids from the sub-inventory
    /// </summary>
    public sealed class TrainInventoryBodyModel : ITrainInventoryBodySource, IInitializable
    {
        private readonly IBlockInventoryPanelSource _panels;
        private readonly SubInventoryState _subInventoryState;
        private readonly ReactiveProperty<TrainInventoryBodyViewModel> _current = new(TrainInventoryBodyViewModel.Closed);

        public IReadOnlyReactiveProperty<TrainInventoryBodyViewModel> Current => _current;

        public TrainInventoryBodyModel(IBlockInventoryPanelSource panels, SubInventoryState subInventoryState)
        {
            _panels = panels;
            _subInventoryState = subInventoryState;
        }

        public void Initialize()
        {
            _panels.Current.Subscribe(_ => Rebuild());
            _subInventoryState.OnSubInventoryUpdated.Subscribe(_ => Rebuild());
            Rebuild();
        }

        private void Rebuild()
        {
            // エラー無しの列車のパネルのあいだだけ中身を配る（エラー行は alpha が出す）
            // Contents publish only while an error-free train panel is open (alpha shows the error row)
            if (_panels.Current.Value is not OpenTrainInventoryPanel { Error: BlockInventoryPanelError.None } open)
            {
                _current.Value = TrainInventoryBodyViewModel.Closed;
                return;
            }
            var fluids = _subInventoryState.CurrentSubInventory?.Fluids;
            // silent-ok: 列車を開いた直後は液体の応答がまだ無く、液体なしで描いて届いた OnSubInventoryUpdated で描き直す / 応答が来ないままなら液体貨車の行が出ず、録画 train/fluid-inventory-via-interact の液体の主張が落ちて開発者が気づく
            _current.Value = new TrainInventoryBodyViewModel(open.ItemSlots, fluids == null ? new List<BlockFluidSlotState>() : TrainCarFluidConversion.ToFluids(fluids));
        }
    }
}
