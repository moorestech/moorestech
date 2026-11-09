using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.Ui.State.BlockDetail;
using Client.Ui.State.Routing;
using Server.Protocol.PacketResponse;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.BlockInventory.Panel
{
    // パネルの状態を読む面（Model とテストの偽物が実装する）
    // Read surface of the panel state (implemented by the model and test fakes)
    public interface IBlockInventoryPanelSource
    {
        IReadOnlyReactiveProperty<BlockInventoryPanelState> Current { get; }
    }

    /// <summary>
    /// SubInventory の源と追跡ブロックを購読してパネルの状態を配る（Web ホスト BlockInventoryTopic の開閉と再計算の契機の写し）
    /// Subscribes to the sub-inventory sources and tracked block to publish the panel state (port of the Web host BlockInventoryTopic open rule and triggers)
    /// </summary>
    public sealed class BlockInventoryPanelModel : IBlockInventoryPanelSource, IInitializable
    {
        private readonly UiRoutingState _routing;
        private readonly SubInventoryState _subInventoryState;
        private readonly OpenBlockDetailTracker _tracker;
        private readonly ReactiveProperty<BlockInventoryPanelState> _current = new(BlockInventoryPanelState.Closed);

        public IReadOnlyReactiveProperty<BlockInventoryPanelState> Current => _current;

        public BlockInventoryPanelModel(UiRoutingState routing, SubInventoryState subInventoryState, OpenBlockDetailTracker tracker)
        {
            _routing = routing;
            _subInventoryState = subInventoryState;
            _tracker = tracker;
        }

        public void Initialize()
        {
            // 画面の切替・スロット更新・追跡替え・追跡ブロックの状態変化で組み直す（ホストと同じ契機。区画の詳細は各レーンの詳細 Model が配る）
            // Rebuild on screen changes, slot updates, tracking changes and tracked-block state changes (host triggers; section details come from each lane's detail model)
            _routing.Current.Subscribe(_ => Rebuild());
            _subInventoryState.OnSubInventoryUpdated.Subscribe(_ => Rebuild());
            _tracker.OnTrackedBlockChanged.Subscribe(_ => Rebuild());
            _tracker.OnTrackedBlockStateChanged.Subscribe(_ => Rebuild());
            Rebuild();

            #region Internal

            void Rebuild()
            {
                _current.Value = Read();
            }

            BlockInventoryPanelState Read()
            {
                // 画面の判定は UiRoutingState に一本化する。SubInventory かつサブインベントリがあるときだけ開く
                // Screen decisions go through UiRoutingState only; open only in SubInventory with a sub-inventory present
                var sub = _subInventoryState.CurrentSubInventory;
                if (_routing.Current.Value.Screen != UiScreen.SubInventory || sub == null) return BlockInventoryPanelState.Closed;
                var slots = BlockInventoryPanelStateFactory.ToSlotItems(sub.SubInventory);
                if (_subInventoryState.CurrentSubInventorySource is TrainSubInventorySource train) return BlockInventoryPanelStateFactory.FromTrain(train.TrainCarInstanceId, slots, train.LastOpenMessage);

                // silent-ok: 追跡ブロックが未解決（撤去直後など）は閉と同じ見え方（ホストと同じ） / 追跡替えの購読で直ちに組み直す
                var block = _tracker.TrackedBlock;
                if (block == null) return BlockInventoryPanelState.Closed;
                var succeeded = sub.Result == InventoryRequestResult.Success;
                return BlockInventoryPanelStateFactory.FromBlock(succeeded, slots, block.BlockMasterElement, block.BlockInstanceId, _tracker.TrackedSource.BlockPosition);
            }

            #endregion
        }
    }
}
