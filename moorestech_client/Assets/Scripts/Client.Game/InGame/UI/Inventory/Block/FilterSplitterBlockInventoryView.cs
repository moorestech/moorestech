using System;
using System.Collections.Generic;
using System.Threading;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Common;
using Client.Game.InGame.UI.Inventory.Main;
using Core.Item.Interface;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.FilterSplitter;
using Game.Context;
using Game.PlayerInventory.Interface.Subscription;
using Server.Protocol.PacketResponse;
using UniRx;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.UI.Inventory.Block
{
    /// <summary>
    /// フィルター分岐器UIのルートビュー。出力方向ごとのColumnを生成してサーバー状態と同期する。
    /// Root view for filter splitter UI; instantiates per-direction columns and syncs with server state.
    /// </summary>
    public class FilterSplitterBlockInventoryView : MonoBehaviour, IBlockInventoryView
    {
        [SerializeField] private Transform columnsParent;
        [SerializeField] private FilterSplitterDirectionColumnView columnPrefab;

        [Inject] private LocalPlayerInventoryController _playerInventory;

        public IReadOnlyList<ItemSlotView> SubInventorySlotObjects => Array.Empty<ItemSlotView>();
        public int Count => 0;
        public List<IItemStack> SubInventory { get; } = new();
        // フィルター分岐器はプレイヤーインベントリ移動の対象外なので識別子は不要
        // Filter splitter is not a target of player-inventory item moves, so identifier is unused
        public ISubInventoryIdentifier ISubInventoryIdentifier { get; } = null;

        private readonly List<FilterSplitterDirectionColumnView> _columns = new();
        private readonly CompositeDisposable _subscriptions = new();
        private CancellationTokenSource _cts;
        private Vector3Int _blockPosition;

        public void Initialize(BlockGameObject blockGameObject)
        {
            _blockPosition = blockGameObject.BlockPosInfo.OriginalPos;

            _cts = new CancellationTokenSource();
            LoadStateAsync(_cts.Token).Forget();
        }

        // フィルター分岐器は通常のインベントリを持たないため空実装
        // Filter splitter doesn't have a normal inventory, so no-op
        public void UpdateItemList(List<IItemStack> items) { }
        public void UpdateInventorySlot(int slot, IItemStack item) { }

        public void DestroyUI()
        {
            // 順序: in-flight タスクを Cancel → UniRx 購読破棄 → CTS Dispose → column 参照クリア → GameObject 破棄
            // Order: cancel in-flight → dispose subscriptions → dispose CTS → release column refs → destroy GameObject
            _cts?.Cancel();
            _subscriptions.Dispose();
            _cts?.Dispose();
            _cts = null;
            _columns.Clear();
            Destroy(gameObject);
        }

        private async UniTask LoadStateAsync(CancellationToken ct)
        {
            var request = FilterSplitterStateProtocol.FilterSplitterStateRequest.CreateGetRequest(_blockPosition);
            var response = await ClientContext.VanillaApi.Response.SendFilterSplitterStateRequest(request, ct);
            if (ct.IsCancellationRequested) return;
            if (response == null || !response.Success)
            {
                Debug.LogError($"FilterSplitter state fetch failed. Reason:{response?.FailureReason}");
                return;
            }

            BuildColumns(response.DirectionCount, response.FilterSlotCountPerDirection);
            ApplySnapshot(response);
        }

        private void BuildColumns(int directionCount, int filterSlotCount)
        {
            for (var d = 0; d < directionCount; d++)
            {
                var column = Instantiate(columnPrefab, columnsParent);
                column.Build(d, filterSlotCount);
                var directionIndex = d;

                column.OnModeCycleRequested
                    .Subscribe(nextMode => OnModeCycleRequested(directionIndex, nextMode).Forget())
                    .AddTo(_subscriptions);
                column.OnFilterSlotClicked
                    .Subscribe(args => OnFilterSlotClicked(directionIndex, args.slotIndex, args.isLeftClick).Forget())
                    .AddTo(_subscriptions);

                _columns.Add(column);
            }
        }

        private void ApplySnapshot(FilterSplitterStateProtocol.FilterSplitterStateResponse response)
        {
            for (var d = 0; d < _columns.Count && d < response.Directions.Count; d++)
            {
                var dir = response.Directions[d];
                _columns[d].ApplyState(dir.Mode, dir.FilterItemIds ?? (IReadOnlyList<ItemId>)Array.Empty<ItemId>());
            }
        }

        private async UniTaskVoid OnModeCycleRequested(int directionIndex, FilterSplitterMode nextMode)
        {
            // DestroyUI 後の Subject 通知で _cts が null になりうるため、ローカルでキャプチャしてから使う
            // Cache _cts into a local in case DestroyUI nulls it before/during the async call
            var cts = _cts;
            if (cts == null) return;
            var request = FilterSplitterStateProtocol.FilterSplitterStateRequest.CreateSetModeRequest(_blockPosition, directionIndex, nextMode);
            var response = await ClientContext.VanillaApi.Response.SendFilterSplitterStateRequest(request, cts.Token);
            if (cts.IsCancellationRequested) return;
            if (response == null || !response.Success) return;
            ApplySnapshot(response);
        }

        private async UniTaskVoid OnFilterSlotClicked(int directionIndex, int slotIndex, bool isLeftClick)
        {
            // DestroyUI 後の Subject 通知で _cts が null になりうるため、ローカルでキャプチャしてから使う
            // Cache _cts into a local in case DestroyUI nulls it before/during the async call
            var cts = _cts;
            if (cts == null) return;

            // 左クリック: 持ち手アイテムをそのままフィルターに設定 / 右クリック: スロットをクリア
            // Left click: set the currently held item as filter / Right click: clear the slot
            ItemId itemId;
            if (isLeftClick)
            {
                var grab = _playerInventory?.GrabInventory;
                if (grab == null || grab.Id == ItemMaster.EmptyItemId) return;
                itemId = grab.Id;
            }
            else
            {
                itemId = ItemMaster.EmptyItemId;
            }

            var request = FilterSplitterStateProtocol.FilterSplitterStateRequest.CreateSetFilterItemRequest(_blockPosition, directionIndex, slotIndex, itemId);
            var response = await ClientContext.VanillaApi.Response.SendFilterSplitterStateRequest(request, cts.Token);
            if (cts.IsCancellationRequested) return;
            if (response == null || !response.Success) return;
            ApplySnapshot(response);
        }
    }
}
