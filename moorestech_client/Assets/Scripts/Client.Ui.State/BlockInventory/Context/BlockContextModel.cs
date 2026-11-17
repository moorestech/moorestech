using Client.Ui.State.BlockDetail;
using Client.Ui.State.BlockDetail.CleanRoom;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Logic.CleanRoom;
using Client.Ui.State.Diagnostics;
using Game.Block.Interface.State;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace Client.Ui.State.BlockInventory.Context
{
    /// <summary>
    /// 部屋・抽選・詰まりの 3 欄を更新のたび配り直す
    /// Re-publishes room, draw and stall entries on updates
    /// </summary>
    public sealed class BlockContextModel : IBlockContextModel, IInitializable
    {
        private readonly OpenBlockDetailTracker _tracker;
        private readonly BlockNetworkInfoCache _cache;
        private readonly ReactiveProperty<BlockContextViewModel> _current = new(BlockContextViewModel.None);
        // 3 欄は同時に別の異常を持ちうるので、欄ごとに旗を分ける（毎 Rebuild で同じ異常を出さない）
        // Each entry can hold its own anomaly at the same time, so each has its own flag (no repeat log per Rebuild)
        private readonly AnomalyReportGate _cleanRoomAnomalyGate = new("", "", LogType.Error);
        private readonly AnomalyReportGate _yieldAnomalyGate = new("", "", LogType.Error);
        private readonly AnomalyReportGate _stallAnomalyGate = new("", "", LogType.Error);

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
                _cleanRoomAnomalyGate.Observe(null);
                _yieldAnomalyGate.Observe(null);
                _stallAnomalyGate.Observe(null);
                _current.Value = BlockContextViewModel.None;
                return;
            }
            _current.Value = new BlockContextViewModel(BuildCleanRoom(), Yield(), Stall());

            #region Internal

            SectionContent<CleanRoomFetchedDetail> BuildCleanRoom()
            {
                // 部屋を追わないなら取得状態も読まない
                // Not following rooms skips the fetch state too
                if (_cache.CleanRoomTarget == CleanRoomDetailTarget.None)
                {
                    _cleanRoomAnomalyGate.Observe(null);
                    return SectionContent<CleanRoomFetchedDetail>.Absent;
                }
                var fetchState = _cache.GetCleanRoom(out var view);
                var operability = block.GetStateDetail<CleanRoomOperabilityStateDetail>(CleanRoomOperabilityStateDetail.BlockStateDetailKey);
                var roomEntry = CleanRoomViewConversion.Build(_cache.CleanRoomTarget, fetchState, view, operability, out var anomaly);
                _cleanRoomAnomalyGate.Observe(anomaly);
                return roomEntry;
            }

            SectionContent<CleanRoomYieldFetched> Yield()
            {
                if (_cache.CleanRoomTarget != CleanRoomDetailTarget.Machine)
                {
                    _yieldAnomalyGate.Observe(null);
                    return SectionContent<CleanRoomYieldFetched>.Absent;
                }
                var fetchState = _cache.GetCleanRoomYield(out var view);
                var yieldEntry = ContextViewConversion.Yield(fetchState, view, out var anomaly);
                _yieldAnomalyGate.Observe(anomaly);
                return yieldEntry;
            }

            SectionContent<MachineInputStall> Stall()
            {
                if (!_cache.IsTrackingMachineInputStall)
                {
                    _stallAnomalyGate.Observe(null);
                    return SectionContent<MachineInputStall>.Absent;
                }
                var fetchState = _cache.GetMachineInputStall(out var view);
                var stallEntry = ContextViewConversion.Stall(fetchState, view, out var anomaly);
                _stallAnomalyGate.Observe(anomaly);
                return stallEntry;
            }

            #endregion
        }
    }
}
