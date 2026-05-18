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

namespace Game.Block.Blocks.FilterSplitter
{
    /// <summary>
    /// 出力方向ごとにアイテムをフィルタリングして分岐するブロック。
    /// 受け取ったアイテムは出力方向ごとの内部バッファ(1スロット)を経由し、
    /// Updateで実出力先へ送出される。出力方向の選択はラウンドロビン。
    /// Filter splitter that routes incoming items per direction with a per-direction 1-slot buffer.
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
            _filterSlotCount = filterSlotCountPerDirection;
            _directions = new DirectionState[outputConnectorElements.Length];
            for (var i = 0; i < outputConnectorElements.Length; i++)
            {
                _directions[i] = new DirectionState(outputConnectorElements[i].ConnectorGuid, filterSlotCountPerDirection);
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

            // 保存データの方向数とマスタの方向数が異なる場合は、両者の最小数まで復元
            // Restore up to the minimum of saved direction count and master direction count
            var count = Math.Min(saveData.Directions.Count, _directions.Length);
            for (var i = 0; i < count; i++)
            {
                _directions[i].LoadFromJson(saveData.Directions[i]);
            }
        }

        #region IBlockInventory

        public IItemStack InsertItem(IItemStack itemStack, InsertItemContext context)
        {
            BlockException.CheckDestroy(this);

            if (itemStack.Count <= 0 || itemStack.Id == ItemMaster.EmptyItemId) return itemStack;

            // フィルター適合かつバッファ空きのある方向をラウンドロビンで決定
            // Find the next eligible direction (filter match + empty buffer) via round-robin
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
                if (FindAnyEligibleDirection(stack.Id) >= 0) return true;
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
            _directions[slot].BufferedItem = itemStack;
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
                dir.BufferedItem = result.Id == ItemMaster.EmptyItemId ? null : result;
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

        public ItemId GetFilterItem(int directionIndex, int slotIndex)
        {
            BlockException.CheckDestroy(this);
            return _directions[directionIndex].FilterItems[slotIndex];
        }

        public void SetFilterItem(int directionIndex, int slotIndex, ItemId itemId)
        {
            BlockException.CheckDestroy(this);
            var dir = _directions[directionIndex];
            // 旧アイテムをセットからも除外
            // Remove old item from set as well
            var prev = dir.FilterItems[slotIndex];
            if (prev != ItemMaster.EmptyItemId) dir.FilterItemSet.Remove(prev);
            dir.FilterItems[slotIndex] = itemId;
            if (itemId != ItemMaster.EmptyItemId) dir.FilterItemSet.Add(itemId);
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

        #region Internal Routing

        private int SelectNextDirection(ItemId itemId)
        {
            var count = _directions.Length;
            if (count == 0) return -1;

            for (var step = 1; step <= count; step++)
            {
                var index = (_roundRobinIndex + step) % count;
                if (IsDirectionEligible(index, itemId))
                {
                    _roundRobinIndex = index;
                    return index;
                }
            }
            return -1;
        }

        private int FindAnyEligibleDirection(ItemId itemId)
        {
            for (var i = 0; i < _directions.Length; i++)
            {
                if (IsDirectionEligible(i, itemId)) return i;
            }
            return -1;
        }

        private bool IsDirectionEligible(int index, ItemId itemId)
        {
            var dir = _directions[index];
            if (dir.BufferedItem != null) return false;

            switch (dir.Mode)
            {
                case FilterSplitterMode.Default:
                    return true;
                case FilterSplitterMode.Whitelist:
                    return dir.FilterItemSet.Contains(itemId);
                case FilterSplitterMode.Blacklist:
                    return !dir.FilterItemSet.Contains(itemId);
                default:
                    return false;
            }
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

        #endregion

        #region Direction State

        private class DirectionState
        {
            public readonly Guid ConnectorGuid;
            public readonly ItemId[] FilterItems;
            public readonly HashSet<ItemId> FilterItemSet = new();
            public FilterSplitterMode Mode = FilterSplitterMode.Default;
            public IItemStack BufferedItem;

            public DirectionState(Guid connectorGuid, int slotCount)
            {
                ConnectorGuid = connectorGuid;
                FilterItems = new ItemId[slotCount];
                for (var i = 0; i < slotCount; i++) FilterItems[i] = ItemMaster.EmptyItemId;
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
                    Mode = (int)Mode,
                    FilterItemGuids = itemGuids,
                    BufferedItem = BufferedItem == null ? null : new ItemStackSaveJsonObject(BufferedItem),
                };
            }

            public void LoadFromJson(DirectionSaveJsonObject json)
            {
                Mode = (FilterSplitterMode)json.Mode;
                FilterItemSet.Clear();
                if (json.FilterItemGuids != null)
                {
                    var count = Math.Min(json.FilterItemGuids.Count, FilterItems.Length);
                    for (var i = 0; i < count; i++)
                    {
                        var guidStr = json.FilterItemGuids[i];
                        if (string.IsNullOrEmpty(guidStr) || !Guid.TryParse(guidStr, out var guid) || guid == Guid.Empty)
                        {
                            FilterItems[i] = ItemMaster.EmptyItemId;
                            continue;
                        }
                        var idOrNull = MasterHolder.ItemMaster.GetItemIdOrNull(guid);
                        if (idOrNull == null)
                        {
                            FilterItems[i] = ItemMaster.EmptyItemId;
                            continue;
                        }
                        FilterItems[i] = idOrNull.Value;
                        FilterItemSet.Add(idOrNull.Value);
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
            [JsonProperty("mode")] public int Mode { get; set; }
            [JsonProperty("filterItemGuids")] public List<string> FilterItemGuids { get; set; }
            [JsonProperty("bufferedItem")] public ItemStackSaveJsonObject BufferedItem { get; set; }
        }

        #endregion
    }
}
