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
    /// 追跡ブロックの液体の列を、追跡の 2 つの通知とパイプの複製の変化で配り直す
    /// Republishes the tracked block's fluid list on the tracker's two notifications and on pipe replica changes
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

        // 追跡の通知のたびに、同値でも新しい VM を配り、パイプの購読を張り替える
        // On each tracker notification publish a new view model even when equal, and re-wire the pipe subscription
        private void OnTrackerNotified()
        {
            _replicaSubscription?.Dispose();
            _replicaSubscription = null;

            // 追跡ブロックが無い（閉・貨車）ときは空の列・進捗なし
            // With no tracked block (closed or freight car) the list is empty with no progress
            var block = _tracker.TrackedBlock;
            if (block == null)
            {
                _current.Value = Empty();
                return;
            }

            _current.Value = BlockFluidSlotReading.Read(block, _replica);
            // 液体タンクの詳細を持たない（パイプかもしれない）ブロックは、節の有無にかかわらず追跡中ずっと複製の変化を購読する
            // A block without a fluid tank detail (possibly a pipe) subscribes to replica changes for the whole tracking, whether or not a node exists yet
            if (block.GetStateDetail<FluidMachineInventoryStateDetail>(FluidMachineInventoryStateDetail.BlockStateDetailKey) != null) return;
            var position = block.BlockPosInfo.OriginalPos;
            _shownHasNode = _replica.TryReadNode(position, out _shownFluidId, out _shownAmount, out _);
            _replicaSubscription = _replica.OnReplicaChanged.Subscribe(_ => OnReplicaChanged(block, position));
        }

        // 毎 tick の通知を、節の有無・液体 id・量のどれかが変わった回だけに絞る（Web ホスト HasPipeContentsChanged と同じ比べ方）
        // Narrow the per-tick notifications to those where node presence, fluid id or amount changed (same comparison as the Web host's HasPipeContentsChanged)
        private void OnReplicaChanged(BlockGameObject block, Vector3Int position)
        {
            var hasNode = _replica.TryReadNode(position, out var fluidId, out var amount, out _);
            // 節の有無・液体 id・量が同じなら配り直さない
            // Skip republishing when node presence, fluid id and amount are unchanged
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
