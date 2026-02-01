using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Connector;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Mooresmaster.Model.BlockConnectInfoModule;
using Mooresmaster.Model.InventoryConnectsModule;
using Newtonsoft.Json;

namespace Game.Block.Blocks.BeltConveyor
{
    /// <summary>
    ///     アイテムの搬出入とインベントリの管理を行う
    /// </summary>
    public class VanillaBeltConveyorComponent : IBlockInventory, IBlockSaveState, IItemCollectableBeltConveyor, IUpdatableBlockComponent
    {
        public BeltConveyorSlopeType SlopeType { get; }
        public IReadOnlyList<IOnBeltConveyorItem> BeltConveyorItems => _inventoryItems;
        private readonly VanillaBeltConveyorInventoryItem[] _inventoryItems;

        private readonly IBeltConveyorBlockInventoryInserter _blockInventoryInserter;
        private readonly int _inventoryItemNum;

        // ベルトコンベアにアイテムが入って出るまでのtick数
        // Ticks for item to enter and exit the belt conveyor
        private uint _ticksOfItemEnterToExit;

        public VanillaBeltConveyorComponent(int inventoryItemNum, float timeOfItemEnterToExitSeconds, IBeltConveyorBlockInventoryInserter blockInventoryInserter, BeltConveyorSlopeType slopeType)
        {
            SlopeType = slopeType;
            _inventoryItemNum = inventoryItemNum;
            _ticksOfItemEnterToExit = GameUpdater.SecondsToTicks(timeOfItemEnterToExitSeconds);
            _blockInventoryInserter = blockInventoryInserter;

            _inventoryItems = new VanillaBeltConveyorInventoryItem[inventoryItemNum];
        }

        public VanillaBeltConveyorComponent(Dictionary<string, string> componentStates, int inventoryItemNum, float timeOfItemEnterToExitSeconds, IBeltConveyorBlockInventoryInserter blockInventoryInserter, BeltConveyorSlopeType slopeType, InventoryConnects inventoryConnectors) :
            this(inventoryItemNum, timeOfItemEnterToExitSeconds, blockInventoryInserter, slopeType)
        {
            var itemJsons = JsonConvert.DeserializeObject<List<string>>(componentStates[SaveKey]);
            for (var i = 0; i < itemJsons.Count && i < inventoryItemNum; i++)
            {
                if (itemJsons[i] != null)
                {
                    _inventoryItems[i] = VanillaBeltConveyorInventoryItem.LoadItem(itemJsons[i], inventoryConnectors, _ticksOfItemEnterToExit);
                }
            }
        }

        public IItemStack InsertItem(IItemStack itemStack, InsertItemContext context)
        {
            BlockException.CheckDestroy(this);

            // 挿入可能スロットを決定する
            // Decide which slot can accept the item
            var insertIndex = GetInsertIndex();
            if (insertIndex < 0) return itemStack;

            var checkItems = new List<IItemStack> { ServerContext.ItemStackFactory.Create(itemStack.Id, 1, itemStack.ItemInstanceId) };

            // GoalConnectorを取得する
            // Get GoalConnector
            var goalConnector = _blockInventoryInserter.GetNextGoalConnector(checkItems);

            // コネクターが存在するのにGoalConnectorがない場合は挿入を拒否する
            // Reject insertion when connectors exist but no valid GoalConnector
            if (_blockInventoryInserter.HasAnyConnector && goalConnector == null) return itemStack;

            // 挿入先コネクター（TargetConnector）をアイテムの開始位置として設定
            // Set target connector as item's start position
            var startConnector = context.TargetConnector;
            _inventoryItems[insertIndex] = new VanillaBeltConveyorInventoryItem(itemStack.Id, itemStack.ItemInstanceId, startConnector, goalConnector, _ticksOfItemEnterToExit);

            // 挿入したのでアイテムを減らして返す
            // Return item with count reduced by 1
            return itemStack.SubItem(1);
            
            #region Internal
            
            int GetInsertIndex()
            {
                // コネクターがある場合は空きスロットを探す
                // Find any empty slot when connectors exist
                if (_blockInventoryInserter.HasAnyConnector)
                {
                    for (var i = _inventoryItems.Length - 1; i >= 0; i--)
                    {
                        if (_inventoryItems[i] == null) return i;
                    }
                    return -1;
                }
                
                // コネクターがない場合は入口スロットのみを許可する
                // When no connector, allow only the entry slot
                if (_inventoryItems[^1] == null) return _inventoryItems.Length - 1;
                return -1;
            }
            
            #endregion
        }
        
        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            BlockException.CheckDestroy(this);

            // 挿入スロットが1個かどうか
            // Check if input is exactly one item
            if (itemStacks.Count != 1 || itemStacks[0].Count != 1) return false;

            // 接続先がない場合は入口スロットのみ確認する
            // When no connectors, check only entry slot
            if (!_blockInventoryInserter.HasAnyConnector) return _inventoryItems[^1] == null;

            // 接続先がある場合は常に受け入れ可能（詰まりを許容する）
            // When connectors exist, always accept (allow clogging)
            return true;
        }

        public int GetSlotSize()
        {
            BlockException.CheckDestroy(this);
            
            return _inventoryItems.Length;
        }
        
        public IItemStack GetItem(int slot)
        {
            BlockException.CheckDestroy(this);
            
            var itemStackFactory = ServerContext.ItemStackFactory;
            if (_inventoryItems[slot] == null) return itemStackFactory.CreatEmpty();
            return itemStackFactory.Create(_inventoryItems[slot].ItemId, 1);
        }
        
        public void SetItem(int slot, IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);

            //TODO lockすべき？？
            var goalConnector = _blockInventoryInserter?.GetNextGoalConnector(new List<IItemStack> { itemStack });
            _inventoryItems[slot] = new VanillaBeltConveyorInventoryItem(itemStack.Id, itemStack.ItemInstanceId, null, goalConnector, _ticksOfItemEnterToExit);
        }
        
        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
        
        public string SaveKey { get; } = typeof(VanillaBeltConveyorComponent).FullName;
        public string GetSaveState()
        {
            BlockException.CheckDestroy(this);
            
            var saveItems = new List<string>();
            foreach (var t in _inventoryItems)
            {
                saveItems.Add(t?.GetSaveJsonString());
            }
            
            return JsonConvert.SerializeObject(saveItems);
        }
        
        /// <summary>
        ///     アイテムの搬出判定を行う
        ///     判定はUpdateで毎フレーム行われる
        ///     TODO 個々のマルチスレッド対応もいい感じにやりたい
        /// </summary>
        public void Update()
        {
            BlockException.CheckDestroy(this);

            //TODO lockすべき？？
            var count = _inventoryItems.Length;

            for (var i = 0; i < count; i++)
            {
                var item = _inventoryItems[i];
                if (item == null) continue;

                // コネクターの存在確認とフォールバック処理
                // Validate connector and fallback if necessary
                ValidateAndUpdateGoalConnector(item);

                // 次のインデックスに入れる時間かどうかをチェックする（tick単位）
                // Check if it's time to move to next index (in ticks)
                var ticksPerSlot = _ticksOfItemEnterToExit / (uint)_inventoryItemNum;
                var nextIndexStartTicks = (uint)i * ticksPerSlot;
                var isNextInsertable = item.RemainingTicks <= nextIndexStartTicks;

                // 次に空きがあれば次に移動する
                // Move to next slot if available
                var didMove = false;
                if (isNextInsertable && i != 0)
                {
                    if (_inventoryItems[i - 1] == null)
                    {
                        _inventoryItems[i - 1] = item;
                        _inventoryItems[i] = null;
                        didMove = true;
                    }
                }

                // 最後のアイテムの場合は接続先に渡す
                // Pass to connected block if item reached the end
                if (i == 0 && item.RemainingTicks == 0)
                {
                    var insertItem = ServerContext.ItemStackFactory.Create(item.ItemId, 1, item.ItemInstanceId);

                    var output = _blockInventoryInserter.InsertItem(insertItem, item.GoalConnector);

                    // 渡した結果がnullItemだったらそのアイテムを消す
                    // Remove item if successfully inserted
                    if (output.Id == ItemMaster.EmptyItemId) _inventoryItems[i] = null;

                    continue;
                }

                // 前のスロットが詰まっているかどうかを判定する（移動した場合は除く）
                // Check if blocked by previous slot (exclude if just moved)
                var isBlockedByPreviousSlot = !didMove && i != 0 && _inventoryItems[i - 1] != null && isNextInsertable;

                // 残りtick数を減らす（詰まっていない場合のみ）
                // Decrease remaining ticks (only when not blocked)
                if (!isBlockedByPreviousSlot)
                {
                    var ticksToSubtract = GameUpdater.CurrentTickCount;
                    if (item.RemainingTicks > ticksToSubtract)
                    {
                        item.RemainingTicks -= ticksToSubtract;
                    }
                    else
                    {
                        item.RemainingTicks = 0;
                    }
                }
            }

            #region Internal

            void ValidateAndUpdateGoalConnector(VanillaBeltConveyorInventoryItem targetItem)
            {
                // 全てのコネクターがなくなった場合は現在の設定を保持
                // Keep current setting if all connectors are gone
                if (_blockInventoryInserter.ConnectedCount == 0) return;

                // 現在のGoalConnectorが無効なら、Guid解決を試してからフォールバック
                // Resolve by Guid before fallback when current GoalConnector is invalid
                if (_blockInventoryInserter.IsValidGoalConnector(targetItem.GoalConnector)) return;

                // 挿入可能な接続先を探す（インデックスを進めない）
                // Find insertable connector (without advancing index)
                var checkItems = new List<IItemStack> { ServerContext.ItemStackFactory.Create(targetItem.ItemId, 1, targetItem.ItemInstanceId) };
                var goalConnector = _blockInventoryInserter.PeekNextGoalConnector(checkItems);
                if (goalConnector == null) return;
                targetItem.SetGoalConnector(goalConnector);
            }

            #endregion
        }

        /// <summary>
        /// ベルトコンベアの速度をtick単位で設定
        /// Set belt conveyor speed in ticks
        /// </summary>
        public void SetTicksOfItemEnterToExit(uint ticks)
        {
            _ticksOfItemEnterToExit = ticks;

            // 有効な速度が設定された場合、停止中に投入されたアイテムのtickを更新
            // When valid speed is set, update ticks of items inserted while stopped
            if (ticks != 0 && ticks != uint.MaxValue)
            {
                foreach (var item in _inventoryItems)
                {
                    item?.ResetTicksOnSpeedRecovery(ticks);
                }
            }
        }
    }
}
