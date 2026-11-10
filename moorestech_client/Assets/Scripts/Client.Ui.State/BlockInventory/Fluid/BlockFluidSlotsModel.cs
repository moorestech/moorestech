using System;
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Game.Block.Interface.State;
using Client.Ui.State.BlockDetail;
using Client.Ui.State.BlockInventory.Logic.Common;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace Client.Ui.State.BlockInventory.Fluid
{
    /// <summary>
    /// 追跡の 2 通知とパイプ複製の変化で液体列を配り直す
    /// Republishes fluids on two tracker notifications and pipe replica
    /// </summary>
    public sealed class BlockFluidSlotsModel : IBlockFluidSlotsModel, IInitializable
    {
        private readonly OpenBlockDetailTracker _tracker;
        private readonly IFluidReplicaReader _replica;
        private readonly ReactiveProperty<BlockFluidSlotsViewModel> _current = new(Empty());
        private IDisposable _replicaSubscription;
        private bool _shownHasNode;
        private int _shownFluidId;
        private double _shownAmount;

        public IReadOnlyReactiveProperty<BlockFluidSlotsViewModel> Current => _current;

        public BlockFluidSlotsModel(OpenBlockDetailTracker tracker, IFluidReplicaReader replica)
        {
            _tracker = tracker;
            _replica = replica;
        }

        public void Initialize()
        {
            _tracker.OnTrackedBlockChanged.Subscribe(_ => OnTrackerNotified());
            _tracker.OnTrackedBlockStateChanged.Subscribe(_ => OnTrackerNotified());
            OnTrackerNotified();
        }

        // 通知のたび新しい VM を配り購読を張り替える
        // Each notification publishes a new VM and rewires
        private void OnTrackerNotified()
        {
            _replicaSubscription?.Dispose();
            _replicaSubscription = null;

            // 追跡ブロックが無ければ空の列
            // With no tracked block (closed or freight car) the list is empty with no progress
            var block = _tracker.TrackedBlock;
            if (block == null)
            {
                _current.Value = Empty();
                return;
            }

            _current.Value = BlockFluidSlotReading.Read(block, _replica);
            // パイプかもしれないブロックは複製を購読
            // A block without a fluid tank detail (possibly a pipe) subscribes to replica changes for the whole tracking
            if (block.GetStateDetail<FluidMachineInventoryStateDetail>(FluidMachineInventoryStateDetail.BlockStateDetailKey) != null) return;
            var position = block.BlockPosInfo.OriginalPos;
            _shownHasNode = _replica.TryReadNode(position, out _shownFluidId, out _shownAmount, out _);
            _replicaSubscription = _replica.OnReplicaChanged.Subscribe(_ => OnReplicaChanged(block, position));
        }

        // 通知を節・液体 id・量の変化だけに絞る
        // Narrow per-tick notifications to changes of node presence, fluid id or amount, as the Web host's HasPipeContentsChanged
        private void OnReplicaChanged(BlockGameObject block, Vector3Int position)
        {
            var hasNode = _replica.TryReadNode(position, out var fluidId, out var amount, out _);
            // 節・液体 id・量が同じなら配らない
            // Skip when node, fluid id and amount match
            if (hasNode == _shownHasNode && fluidId == _shownFluidId && amount == _shownAmount) return;
            _shownHasNode = hasNode;
            _shownFluidId = fluidId;
            _shownAmount = amount;
            _current.Value = BlockFluidSlotReading.Read(block, _replica);
        }

        private static BlockFluidSlotsViewModel Empty()
        {
            return new BlockFluidSlotsViewModel(new List<BlockFluidSlotState>(), false, 0);
        }
    }
}
