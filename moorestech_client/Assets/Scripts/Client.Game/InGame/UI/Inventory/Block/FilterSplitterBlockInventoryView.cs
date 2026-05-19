using System;
using System.Collections.Generic;
using System.Threading;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Main;
using Core.Item.Interface;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.FilterSplitter;
using Game.Context;
using Game.PlayerInventory.Interface.Subscription;
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

        public IReadOnlyList<Client.Game.InGame.UI.Inventory.Common.ItemSlotView> SubInventorySlotObjects => Array.Empty<Client.Game.InGame.UI.Inventory.Common.ItemSlotView>();
        public int Count => 0;
        public List<IItemStack> SubInventory { get; } = new();
        public ISubInventoryIdentifier ISubInventoryIdentifier { get; private set; }

        private readonly List<FilterSplitterDirectionColumnView> _columns = new();
        private readonly CompositeDisposable _subscriptions = new();
        private CancellationTokenSource _cts;
        private Vector3Int _blockPosition;

        public void Initialize(BlockGameObject blockGameObject)
        {
            ISubInventoryIdentifier = new BlockInventorySubInventoryIdentifier(blockGameObject.BlockPosInfo.OriginalPos);
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
            _cts?.Cancel();
            _cts?.Dispose();
            _subscriptions.Dispose();
            Destroy(gameObject);
        }

        private async UniTask LoadStateAsync(CancellationToken ct)
        {
            var response = await ClientContext.VanillaApi.Response.GetFilterSplitterState(_blockPosition, ct);
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

        private void ApplySnapshot(Server.Protocol.PacketResponse.FilterSplitterStateProtocol.FilterSplitterStateResponse response)
        {
            for (var d = 0; d < _columns.Count && d < response.Directions.Count; d++)
            {
                var dir = response.Directions[d];
                _columns[d].ApplyState((FilterSplitterMode)dir.Mode, dir.FilterItemGuids ?? new List<string>());
            }
        }

        private async UniTaskVoid OnModeCycleRequested(int directionIndex, FilterSplitterMode nextMode)
        {
            var ct = _cts?.Token ?? CancellationToken.None;
            var response = await ClientContext.VanillaApi.Response.SetFilterSplitterMode(_blockPosition, directionIndex, nextMode, ct);
            if (response == null || !response.Success) return;
            ApplySnapshot(response);
        }

        private async UniTaskVoid OnFilterSlotClicked(int directionIndex, int slotIndex, bool isLeftClick)
        {
            var ct = _cts?.Token ?? CancellationToken.None;

            // 左クリック: 持ち手アイテムがあればそれをフィルターに設定 / 右クリック: スロットをクリア
            // Left click: if player holds an item, set it as filter / Right click: clear the slot
            Guid itemGuid;
            if (isLeftClick)
            {
                var grab = _playerInventory?.GrabInventory;
                if (grab == null || grab.Id == ItemMaster.EmptyItemId) return;
                itemGuid = MasterHolder.ItemMaster.GetItemMaster(grab.Id).ItemGuid;
            }
            else
            {
                itemGuid = Guid.Empty;
            }

            var response = await ClientContext.VanillaApi.Response.SetFilterSplitterItem(_blockPosition, directionIndex, slotIndex, itemGuid, ct);
            if (response == null || !response.Success) return;
            ApplySnapshot(response);
        }
    }
}
