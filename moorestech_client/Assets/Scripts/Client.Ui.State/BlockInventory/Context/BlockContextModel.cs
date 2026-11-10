using Client.Ui.State.BlockDetail;
using Client.Ui.State.BlockDetail.CleanRoom;
using Client.Ui.State.BlockInventory.Logic.CleanRoom;
using Game.Block.Interface.State;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.BlockInventory.Context
{
    /// <summary>
    /// 部屋・抽選・詰まりの 3 欄を、追跡替え・状態変化・1 秒ポーリングの更新で配り直す
    /// Re-publishes the room, draw and stall entries on tracking changes, state changes and the one-second poll updates
    /// </summary>
    public sealed class BlockContextModel : IBlockContextModel, IInitializable
    {
        private readonly OpenBlockDetailTracker _tracker;
        private readonly BlockNetworkInfoCache _cache;
        private readonly ReactiveProperty<BlockContextViewModel> _current = new(BlockContextViewModel.None);

        public IReadOnlyReactiveProperty<BlockContextViewModel> Current => _current;

        public BlockContextModel(OpenBlockDetailTracker tracker, BlockNetworkInfoCache cache)
        {
            _tracker = tracker;
            _cache = cache;
        }

        public void Initialize()
        {
            _tracker.OnTrackedBlockChanged.Subscribe(_ => Rebuild());
            _tracker.OnTrackedBlockStateChanged.Subscribe(_ => Rebuild());
            _cache.OnUpdated.Subscribe(_ => Rebuild());
            Rebuild();
        }

        private void Rebuild()
        {
            // silent-ok: 追跡ブロックが無いとき 3 欄は無い（閉と同じ） / 追跡替えの購読で直ちに組み直す
            var block = _tracker.TrackedBlock;
            if (block == null)
            {
                _current.Value = BlockContextViewModel.None;
                return;
            }
            _current.Value = new BlockContextViewModel(BuildCleanRoom(), Yield(), Stall());

            #region Internal

            ContextEntry<CleanRoomFetchedDetail> BuildCleanRoom()
            {
                // 部屋の照会を追わないブロックは取得状態も読まない
                // Blocks that do not follow the room query skip reading the fetch state too
                if (_cache.CleanRoomTarget == CleanRoomDetailTarget.None) return ContextEntry<CleanRoomFetchedDetail>.Absent;
                var fetchState = _cache.GetCleanRoom(out var view);
                var operability = block.GetStateDetail<CleanRoomOperabilityStateDetail>(CleanRoomOperabilityStateDetail.BlockStateDetailKey);
                return CleanRoomViewConversion.Build(_cache.CleanRoomTarget, fetchState, view, operability);
            }

            ContextEntry<CleanRoomYieldFetched> Yield()
            {
                if (_cache.CleanRoomTarget != CleanRoomDetailTarget.Machine) return ContextEntry<CleanRoomYieldFetched>.Absent;
                var fetchState = _cache.GetCleanRoomYield(out var view);
                return ContextViewConversion.Yield(fetchState, view);
            }

            ContextEntry<MachineInputStall> Stall()
            {
                if (!_cache.IsTrackingMachineInputStall) return ContextEntry<MachineInputStall>.Absent;
                var fetchState = _cache.GetMachineInputStall(out var view);
                return ContextViewConversion.Stall(fetchState, view);
            }

            #endregion
        }
    }
}
