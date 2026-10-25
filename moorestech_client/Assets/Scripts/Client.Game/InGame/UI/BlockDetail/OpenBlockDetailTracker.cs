using System;
using Client.Game.InGame.Block;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.Network.API;
using Mooresmaster.Model.BlocksModule;
using Server.Event.EventReceive;
using UniRx;
using VContainer.Unity;

namespace Client.Game.InGame.UI.BlockDetail
{
    /// <summary>
    /// サブインベントリで開いているブロックを追い、そのブロック状態イベントの購読とネットワーク情報の取得を張り替える
    /// Tracks the block open in the sub-inventory and re-wires its block-state event subscription and network fetches
    /// </summary>
    public sealed class OpenBlockDetailTracker : IInitializable
    {
        private readonly UIStateControl _uiStateControl;
        private readonly SubInventoryState _subInventoryState;
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;
        private readonly BlockNetworkInfoCache _networkCache;
        private readonly IVanillaApiEvent _vanillaApiEvent;
        private readonly Subject<BlockGameObject> _onTrackedBlockChanged = new();
        private readonly Subject<Unit> _onTrackedBlockStateChanged = new();
        private IDisposable _blockStateSubscription;

        public BlockGameObject TrackedBlock { get; private set; }
        public IObservable<BlockGameObject> OnTrackedBlockChanged => _onTrackedBlockChanged;
        public IObservable<Unit> OnTrackedBlockStateChanged => _onTrackedBlockStateChanged;

        public OpenBlockDetailTracker(UIStateControl uiStateControl, SubInventoryState subInventoryState, BlockGameObjectDataStore blockGameObjectDataStore, BlockNetworkInfoCache networkCache, IVanillaApiEvent vanillaApiEvent)
        {
            _uiStateControl = uiStateControl;
            _subInventoryState = subInventoryState;
            _blockGameObjectDataStore = blockGameObjectDataStore;
            _networkCache = networkCache;
            _vanillaApiEvent = vanillaApiEvent;
        }

        public void Initialize()
        {
            // 開閉（UIステート遷移）とスロット更新のたびに、開いているブロックを解決し直す
            // Re-resolve the open block on every open/close (UI-state transition) and slot update
            _uiStateControl.OnStateChanged.Subscribe(_ => Refresh());
            _subInventoryState.OnSubInventoryUpdated.Subscribe(_ => Refresh());
        }

        // SubInventory 状態かつ発生元がブロックのときだけ追跡する（列車等の非ブロックは追跡しない）
        // Track only in the SubInventory state with a block source (non-block sources like trains are not tracked)
        private void Refresh()
        {
            var open = _uiStateControl.CurrentState == UIStateEnum.SubInventory && _subInventoryState.CurrentSubInventory != null;
            if (open && _subInventoryState.CurrentSubInventorySource is BlockSubInventorySource blockSource && _blockGameObjectDataStore.TryGetBlockGameObject(blockSource.BlockPosition, out var block))
            {
                Track(block);
                return;
            }
            Track(null);
        }

        // 追跡ブロックを切り替え、state イベント購読とネットワーク取得を張り替える
        // Switch the tracked block, re-wiring the state-event subscription and network fetches
        private void Track(BlockGameObject block)
        {
            if (ReferenceEquals(TrackedBlock, block)) return; // silent-ok: 同じ追跡先は購読を維持する / 既存の通知が描画側へ届く
            // 前のブロックの state イベント購読を解除する
            // Unsubscribe the previous block's state event
            _blockStateSubscription?.Dispose();
            _blockStateSubscription = null;
            TrackedBlock = block;
            if (block == null)
            {
                _networkCache.Clear();
                _onTrackedBlockChanged.OnNext(null);
                return;
            }

            // uGUI BlockGameObject と同じ per-block イベントタグを直接購読して再配信トリガにする
            // Directly subscribe the same per-block event tag as uGUI BlockGameObject as the republish trigger
            var eventTag = ChangeBlockStateEventPacket.CreateSpecifiedBlockEventTag(block.BlockPosInfo);
            _blockStateSubscription = _vanillaApiEvent.SubscribeEventResponse(eventTag, _ => _onTrackedBlockStateChanged.OnNext(Unit.Default));
            // ネットワーク所属はマスタの接続paramが答える。blockTypeの列挙は新種別を取りこぼす
            // The master's connection params answer which networks a block joins; enumerating blockTypes drops new kinds
            var param = block.BlockMasterElement.BlockParam;
            var electric = param is IElectricWireConnectParam;
            var gear = param is IGearConnectors;
            var filterSplitter = block.BlockMasterElement.BlockType == "FilterSplitter";
            _networkCache.Track(block, electric, gear, filterSplitter);
            _onTrackedBlockChanged.OnNext(block);
        }
    }
}
