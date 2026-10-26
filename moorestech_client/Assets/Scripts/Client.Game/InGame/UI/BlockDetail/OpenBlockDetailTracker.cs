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
    /// 開いたブロックを追い購読を張替
    /// Tracks the open block and re-wires its subscriptions
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
            // 開閉・スロット更新で再解決
            // Re-resolve on open/close and slot update
            _uiStateControl.OnStateChanged.Subscribe(_ => Refresh());
            _subInventoryState.OnSubInventoryUpdated.Subscribe(_ => Refresh());
            // 開いている間の撤去・同座標の差し替えでも、破棄済みの個体を追わないよう解決し直す
            // Re-resolve on removal or same-position replacement so a destroyed instance is never tracked
            _blockGameObjectDataStore.OnBlockPlaced.Subscribe(_ => Refresh());
            _blockGameObjectDataStore.OnBlockRemoved.Subscribe(_ => Refresh());
        }

        // ブロック発生元の時だけ追跡
        // Track only when the source is a block
        private void Refresh()
        {
            var open = _uiStateControl.CurrentState == UIStateEnum.SubInventory && _subInventoryState.CurrentSubInventory != null;
            if (open && _subInventoryState.CurrentSubInventorySource is BlockSubInventorySource blockSource && _blockGameObjectDataStore.TryGetBlockGameObject(blockSource.BlockPosition, out var block))
            {
                Track(block);
                return;
            }
            Track(null);

            #region Internal

            // 追跡切替で購読と取得を張替
            // Re-wire subscription and fetches on switch
            void Track(BlockGameObject block)
            {
                if (ReferenceEquals(TrackedBlock, block)) return; // silent-ok: 同じ追跡先は購読を維持する / 既存の通知が描画側へ届く
                // 前ブロックの購読解除
                // Unsubscribe the previous block
                _blockStateSubscription?.Dispose();
                _blockStateSubscription = null;
                TrackedBlock = block;
                if (block == null)
                {
                    _networkCache.Clear();
                    _onTrackedBlockChanged.OnNext(null);
                    return;
                }

                // uGUIと同じタグを購読し再配信契機に
                // Subscribe the uGUI event tag as republish trigger
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

            #endregion
        }
    }
}
