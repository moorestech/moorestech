using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Mooresmaster.Model.BlockConnectInfoModule;
using Newtonsoft.Json;
using UnityEngine;

namespace Game.Block.Blocks.FilterSplitter
{
    /// <summary>
    /// 出力方向ごとにアイテムをフィルタリングして分岐するブロック。
    /// Whitelist/Blacklist で明示マッチした方向を優先、Default は fallback として使う。
    /// Filter splitter that routes items per direction. Explicit (Whitelist/Blacklist) directions take priority, Default acts as fallback.
    /// </summary>
    public class VanillaFilterSplitterComponent : IBlockInventory, IBlockSaveState, IUpdatableBlockComponent
    {
        public string SaveKey { get; } = typeof(VanillaFilterSplitterComponent).FullName;
        public bool IsDestroy { get; private set; }

        // 出力方向の数（マスタ上の outputConnects.Length と等価）
        // Number of output directions (equals master's outputConnects length)
        public int DirectionCount => _directions.Length;
        public int FilterSlotCountPerDirection => _filterSlotCount;

        private readonly DirectionState[] _directions;
        private readonly BlockConnectorComponent<IBlockInventory> _connectorComponent;
        private readonly BlockInstanceId _blockInstanceId;
        private readonly int _filterSlotCount;
        private int _roundRobinIndex = -1;

        public VanillaFilterSplitterComponent(
            BlockInstanceId blockInstanceId,
            BlockConnectorComponent<IBlockInventory> connectorComponent,
            BlockConnectInfoElement[] outputConnectorElements,
            int filterSlotCountPerDirection)
        {
            _blockInstanceId = blockInstanceId;
            _connectorComponent = connectorComponent;
            _filterSlotCount = Math.Max(0, filterSlotCountPerDirection);
            _directions = new DirectionState[outputConnectorElements.Length];
            for (var i = 0; i < outputConnectorElements.Length; i++)
            {
                _directions[i] = new DirectionState(outputConnectorElements[i].ConnectorGuid, _filterSlotCount);
            }
        }

        public VanillaFilterSplitterComponent(
            Dictionary<string, string> componentStates,
            BlockInstanceId blockInstanceId,
            BlockConnectorComponent<IBlockInventory> connectorComponent,
            BlockConnectInfoElement[] outputConnectorElements,
            int filterSlotCountPerDirection) :
            this(blockInstanceId, connectorComponent, outputConnectorElements, filterSlotCountPerDirection)
        {
            if (!componentStates.TryGetValue(SaveKey, out var json)) return;
            var saveData = JsonConvert.DeserializeObject<SaveJsonObject>(json);
            if (saveData?.Directions == null) return;

            // 保存データの方向を ConnectorGuid ベースで現方向にマップして復元する
            // Restore each saved direction by mapping its ConnectorGuid onto current directions
            foreach (var savedDir in saveData.Directions)
            {
                if (string.IsNullOrEmpty(savedDir.ConnectorGuid) || !Guid.TryParse(savedDir.ConnectorGuid, out var g))
                {
                    Debug.LogWarning($"FilterSplitter load: invalid connectorGuid '{savedDir.ConnectorGuid}', direction settings dropped");
                    continue;
                }
                var currentIndex = FindDirectionIndexByGuid(g);
                if (currentIndex < 0)
                {
                    Debug.LogWarning($"FilterSplitter load: connectorGuid {g} not found in current master, direction settings dropped");
                    continue;
                }
                _directions[currentIndex].LoadFromJson(savedDir);
            }
        }

        #region IBlockInventory

        public IItemStack InsertItem(IItemStack itemStack, InsertItemContext context)
        {
            BlockException.CheckDestroy(this);

            if (itemStack.Count <= 0 || itemStack.Id == ItemMaster.EmptyItemId) return itemStack;

            // 明示マッチ方向を優先し、なければ Default 方向をラウンドロビン
            // Pick explicit match first, then fall back to Default directions via round-robin
            var directionIndex = SelectNextDirection(itemStack.Id);
            if (directionIndex < 0) return itemStack;

            // 1個だけ受け取ってバッファへ格納（残りは呼び出し元へ返す）
            // Accept exactly one item into the buffer slot; return the rest to caller
            _directions[directionIndex].BufferedItem = ServerContext.ItemStackFactory.Create(itemStack.Id, 1, itemStack.ItemInstanceId);
            return itemStack.SubItem(1);
        }

        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            BlockException.CheckDestroy(this);
            if (itemStacks.Count == 0) return false;

            // 受け入れ可能な方向が1つでもあればtrue
            // Returns true if at least one direction can accept the item
            foreach (var stack in itemStacks)
            {
                if (stack.Id == ItemMaster.EmptyItemId) continue;
                if (0 <= FindAnyEligibleDirection(stack.Id)) return true;
            }
            return false;
        }

        public int GetSlotSize()
        {
            BlockException.CheckDestroy(this);
            return _directions.Length;
        }

        public IItemStack GetItem(int slot)
        {
            BlockException.CheckDestroy(this);
            return _directions[slot].BufferedItem ?? ServerContext.ItemStackFactory.CreatEmpty();
        }

        public void SetItem(int slot, IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);
            // empty / 不正カウントは null 化、count >= 2 は 1 に丸めて格納
            // Normalize: empty/invalid count to null, clamp count to 1
            if (itemStack == null || itemStack.Id == ItemMaster.EmptyItemId || itemStack.Count <= 0)
            {
                _directions[slot].BufferedItem = null;
                return;
            }
            _directions[slot].BufferedItem = itemStack.Count == 1
                ? itemStack
                : ServerContext.ItemStackFactory.Create(itemStack.Id, 1, itemStack.ItemInstanceId);
        }

        #endregion

        #region Update

        public void Update()
        {
            BlockException.CheckDestroy(this);

            // 各方向のバッファアイテムを対応する出力先へ送る
            // Push each direction's buffered item to its corresponding output target
            for (var i = 0; i < _directions.Length; i++)
            {
                var dir = _directions[i];
                if (dir.BufferedItem == null) continue;

                var target = FindConnectedTargetByGuid(dir.ConnectorGuid);
                if (target == null) continue;

                var context = new InsertItemContext(_blockInstanceId, target.Value.Info.SelfConnector, target.Value.Info.TargetConnector);
                var result = target.Value.Inventory.InsertItem(dir.BufferedItem, context);
                // 送信完了は「空 ID」または「Count <= 0」で判定（IItemStack 実装によって戻り値が異なるため両方見る）
                // Treat both "empty id" and "count <= 0" as fully delivered (IItemStack implementations differ)
                dir.BufferedItem = (result.Id == ItemMaster.EmptyItemId || result.Count <= 0) ? null : result;
            }
        }

        #endregion

        #region Save

        public string GetSaveState()
        {
            BlockException.CheckDestroy(this);

            var directions = new List<DirectionSaveJsonObject>();
            foreach (var dir in _directions)
            {
                directions.Add(dir.ToJsonObject());
            }
            return JsonConvert.SerializeObject(new SaveJsonObject { Directions = directions });
        }

        #endregion

        #region Filter Config API

        public FilterSplitterMode GetMode(int directionIndex)
        {
            BlockException.CheckDestroy(this);
            return _directions[directionIndex].Mode;
        }

        public void SetMode(int directionIndex, FilterSplitterMode mode)
        {
            BlockException.CheckDestroy(this);
            _directions[directionIndex].Mode = mode;
        }

        public void SetFilterItem(int directionIndex, int slotIndex, ItemId itemId)
        {
            BlockException.CheckDestroy(this);
            _directions[directionIndex].FilterItems[slotIndex] = itemId;
        }

        public IReadOnlyList<ItemId> GetFilterItems(int directionIndex)
        {
            BlockException.CheckDestroy(this);
            return _directions[directionIndex].FilterItems;
        }

        #endregion

        public void Destroy()
        {
            IsDestroy = true;
        }

        private int SelectNextDirection(ItemId itemId)
        {
            var count = _directions.Length;
            if (count == 0) return -1;

            // 1パス目: Whitelist/Blacklist で明示マッチする方向を優先
            // Pass 1: prefer directions that explicitly match (Whitelist/Blacklist)
            var explicitIndex = TryPickRoundRobin(itemId, requireExplicitMatch: true);
            if (0 <= explicitIndex) return explicitIndex;

            // 2パス目: Default 方向を fallback として選ぶ
            // Pass 2: fall back to Default directions
            return TryPickRoundRobin(itemId, requireExplicitMatch: false);
        }

        private int TryPickRoundRobin(ItemId itemId, bool requireExplicitMatch)
        {
            var count = _directions.Length;
            for (var step = 1; step <= count; step++)
            {
                var index = (_roundRobinIndex + step) % count;
                if (!CanAcceptInDirection(index)) continue;
                var mode = _directions[index].Mode;
                if (requireExplicitMatch)
                {
                    if (mode == FilterSplitterMode.Whitelist && _directions[index].ContainsFilterItem(itemId))
                    {
                        _roundRobinIndex = index;
                        return index;
                    }
                    if (mode == FilterSplitterMode.Blacklist && !_directions[index].ContainsFilterItem(itemId))
                    {
                        _roundRobinIndex = index;
                        return index;
                    }
                }
                else if (mode == FilterSplitterMode.Default)
                {
                    _roundRobinIndex = index;
                    return index;
                }
            }
            return -1;
        }

        private int FindAnyEligibleDirection(ItemId itemId)
        {
            // 明示マッチ方向を優先
            // Prefer explicit match directions
            for (var i = 0; i < _directions.Length; i++)
            {
                if (!CanAcceptInDirection(i)) continue;
                var mode = _directions[i].Mode;
                if (mode == FilterSplitterMode.Whitelist && _directions[i].ContainsFilterItem(itemId)) return i;
                if (mode == FilterSplitterMode.Blacklist && !_directions[i].ContainsFilterItem(itemId)) return i;
            }
            // Default 方向を fallback
            // Default directions as fallback
            for (var i = 0; i < _directions.Length; i++)
            {
                if (!CanAcceptInDirection(i)) continue;
                if (_directions[i].Mode == FilterSplitterMode.Default) return i;
            }
            return -1;
        }

        private bool CanAcceptInDirection(int index)
        {
            var dir = _directions[index];
            // バッファ占有中は受け取らない
            // Skip directions whose buffer is occupied
            if (dir.BufferedItem != null) return false;
            // 接続先が無い方向は受け取らない（永久滞留防止）
            // Skip directions with no connected target (avoid permanent stalling)
            return HasConnection(dir.ConnectorGuid);
        }

        private bool HasConnection(Guid connectorGuid)
        {
            foreach (var pair in _connectorComponent.ConnectedTargets)
            {
                if (pair.Value.SelfConnector != null && pair.Value.SelfConnector.ConnectorGuid == connectorGuid) return true;
            }
            return false;
        }

        private (IBlockInventory Inventory, ConnectedInfo Info)? FindConnectedTargetByGuid(Guid connectorGuid)
        {
            foreach (var pair in _connectorComponent.ConnectedTargets)
            {
                if (pair.Value.SelfConnector != null && pair.Value.SelfConnector.ConnectorGuid == connectorGuid)
                {
                    return (pair.Key, pair.Value);
                }
            }
            return null;
        }

        private int FindDirectionIndexByGuid(Guid connectorGuid)
        {
            for (var i = 0; i < _directions.Length; i++)
            {
                if (_directions[i].ConnectorGuid == connectorGuid) return i;
            }
            return -1;
        }

        private class DirectionState
        {
            public readonly Guid ConnectorGuid;
            public readonly ItemId[] FilterItems;
            public FilterSplitterMode Mode = FilterSplitterMode.Default;
            public IItemStack BufferedItem;

            public DirectionState(Guid connectorGuid, int slotCount)
            {
                ConnectorGuid = connectorGuid;
                FilterItems = new ItemId[slotCount];
                for (var i = 0; i < slotCount; i++) FilterItems[i] = ItemMaster.EmptyItemId;
            }

            public bool ContainsFilterItem(ItemId itemId)
            {
                // 重複登録があっても誤判定にならないよう配列を直接走査
                // Scan the array directly so duplicate entries don't corrupt judgment
                for (var i = 0; i < FilterItems.Length; i++)
                {
                    if (FilterItems[i] == itemId) return true;
                }
                return false;
            }

            public DirectionSaveJsonObject ToJsonObject()
            {
                var itemGuids = new List<string>();
                foreach (var id in FilterItems)
                {
                    if (id == ItemMaster.EmptyItemId)
                    {
                        itemGuids.Add(null);
                    }
                    else
                    {
                        var master = MasterHolder.ItemMaster.GetItemMaster(id);
                        itemGuids.Add(master.ItemGuid.ToString());
                    }
                }
                return new DirectionSaveJsonObject
                {
                    ConnectorGuid = ConnectorGuid.ToString(),
                    Mode = (int)Mode,
                    FilterItemGuids = itemGuids,
                    BufferedItem = BufferedItem == null ? null : new ItemStackSaveJsonObject(BufferedItem),
                };
            }

            public void LoadFromJson(DirectionSaveJsonObject json)
            {
                // 未知 mode 値は Default に fallback
                // Unknown mode value falls back to Default
                Mode = Enum.IsDefined(typeof(FilterSplitterMode), json.Mode)
                    ? (FilterSplitterMode)json.Mode
                    : FilterSplitterMode.Default;
                if (json.FilterItemGuids != null)
                {
                    var saveCount = json.FilterItemGuids.Count;
                    if (FilterItems.Length < saveCount)
                    {
                        Debug.LogWarning($"FilterSplitter load: saved slot count {saveCount} exceeds current master {FilterItems.Length}, extra slots dropped");
                    }
                    var count = Math.Min(saveCount, FilterItems.Length);
                    for (var i = 0; i < count; i++)
                    {
                        var guidStr = json.FilterItemGuids[i];
                        if (string.IsNullOrEmpty(guidStr) || !Guid.TryParse(guidStr, out var guid) || guid == Guid.Empty)
                        {
                            FilterItems[i] = ItemMaster.EmptyItemId;
                            continue;
                        }
                        var idOrNull = MasterHolder.ItemMaster.GetItemIdOrNull(guid);
                        FilterItems[i] = idOrNull ?? ItemMaster.EmptyItemId;
                    }
                }

                BufferedItem = json.BufferedItem?.ToItemStack();
            }
        }

        private class SaveJsonObject
        {
            [JsonProperty("directions")] public List<DirectionSaveJsonObject> Directions { get; set; }
        }

        private class DirectionSaveJsonObject
        {
            [JsonProperty("connectorGuid")] public string ConnectorGuid { get; set; }
            [JsonProperty("mode")] public int Mode { get; set; }
            [JsonProperty("filterItemGuids")] public List<string> FilterItemGuids { get; set; }
            [JsonProperty("bufferedItem")] public ItemStackSaveJsonObject BufferedItem { get; set; }
        }
    }
}
