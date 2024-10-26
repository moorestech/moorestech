using System.Collections.Generic;
using System.Linq;
using Core.Const;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Component;
using Game.Block.Factory.BlockTemplate;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Newtonsoft.Json;
using UniRx;

namespace Game.Block.Blocks.BeltConveyor
{
    /// <summary>
    ///     アイテムの搬出入とインベントリの管理を行う
    /// </summary>
    public class VanillaBeltConveyorComponent : IBlockInventory, IBlockSaveState, IItemCollectableBeltConveyor, IUpdatableBlockComponent
    {
        public const float DefaultBeltConveyorHeight = 0.3f;
        
        public int InventoryItemNum { get; }
        public bool IsDestroy { get; private set; }
        
        public IReadOnlyList<IOnBeltConveyorItem> BeltConveyorItems => _inventoryItems;
        private readonly BeltConveyorInventoryItem[] _inventoryItems;
        
        private readonly BlockConnectorComponent<IBlockInventory> _blockConnectorComponent;
        private readonly string _blockName;
        private double _timeOfItemEnterToExit; //ベルトコンベアにアイテムが入って出るまでの時間
        
        public VanillaBeltConveyorComponent(int inventoryItemNum, float timeOfItemEnterToExit, BlockConnectorComponent<IBlockInventory> blockConnectorComponent, string blockName)
        {
            _blockName = blockName;
            InventoryItemNum = inventoryItemNum;
            _timeOfItemEnterToExit = timeOfItemEnterToExit;
            _blockConnectorComponent = blockConnectorComponent;
            
            _inventoryItems = new BeltConveyorInventoryItem[inventoryItemNum];
        }
        
        public VanillaBeltConveyorComponent(string state, int inventoryItemNum, float timeOfItemEnterToExit, BlockConnectorComponent<IBlockInventory> blockConnectorComponent, string blockName) :
            this(inventoryItemNum, timeOfItemEnterToExit, blockConnectorComponent, blockName)
        {
            //stateから復元
            //データがないときは何もしない
            if (state == string.Empty) return;
            
            List<BeltConveyorItemJsonObject> items = JsonConvert.DeserializeObject<List<BeltConveyorItemJsonObject>>(state);
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i].ItemStack == null) continue;
                
                var itemStack = items[i].ItemStack.ToItemStack();
                _inventoryItems[i] = new BeltConveyorInventoryItem(itemStack.Id, itemStack.ItemInstanceId);
                _inventoryItems[i].RemainingPercent = items[i].RemainingPercent;
            }
        }
        
        public IItemStack InsertItem(IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);
            
            //新しく挿入可能か
            if (_inventoryItems[^1] != null)
                //挿入可能でない
                return itemStack;
            
            _inventoryItems[^1] = new BeltConveyorInventoryItem(itemStack.Id, itemStack.ItemInstanceId);
            
            //挿入したのでアイテムを減らして返す
            return itemStack.SubItem(1);
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
            _inventoryItems[slot] = new BeltConveyorInventoryItem(itemStack.Id, itemStack.ItemInstanceId);
        }
        
        public void Destroy()
        {
            IsDestroy = true;
        }
        
        public string GetSaveState()
        {
            BlockException.CheckDestroy(this);
            
            if (_inventoryItems.Length == 0) return string.Empty;
            
            var saveItems = new List<BeltConveyorItemJsonObject>();
            foreach (var t in _inventoryItems) saveItems.Add(new BeltConveyorItemJsonObject(t));
            
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
            
            if (_blockName == VanillaBeltConveyorTemplate.Hueru && _inventoryItems[0] == null) _inventoryItems[0] = new BeltConveyorInventoryItem(new ItemId(4), ItemInstanceId.Create());
            for (var i = 0; i < count; i++)
            {
                var item = _inventoryItems[i];
                if (item == null) continue;
                
                //次のインデックスに入れる時間かどうかをチェックする
                var nextIndexStartTime = i * (1f / InventoryItemNum);
                var isNextInsertable = item.RemainingPercent <= nextIndexStartTime;
                
                //次に空きがあれば次に移動する
                if (isNextInsertable && i != 0)
                {
                    if (_inventoryItems[i - 1] == null)
                    {
                        _inventoryItems[i - 1] = item;
                        _inventoryItems[i] = null;
                    }
                    
                    continue;
                }
                
                //最後のアイテムの場合は接続先に渡す
                if (i == 0 && item.RemainingPercent <= 0)
                {
                    if (_blockName == VanillaBeltConveyorTemplate.Kieru) _inventoryItems[i] = null;
                    
                    var insertItem = ServerContext.ItemStackFactory.Create(item.ItemId, 1, item.ItemInstanceId);
                    
                    if (_blockConnectorComponent.ConnectedTargets.Count == 0) continue;
                    
                    var connector = _blockConnectorComponent.ConnectedTargets.First();
                    var output = connector.Key.InsertItem(insertItem);
                    
                    //渡した結果がnullItemだったらそのアイテムを消す
                    if (output.Id == ItemMaster.EmptyItemId) _inventoryItems[i] = null;
                    
                    continue;
                }
                
                //時間を減らす 
                item.RemainingPercent -= (float)(GameUpdater.UpdateSecondTime * (1f / (float)_timeOfItemEnterToExit));
            }
        }
        
        public void SetTimeOfItemEnterToExit(double time)
        {
            _timeOfItemEnterToExit = time;
        }
        
        public BeltConveyorInventoryItem GetBeltConveyorItem(int index)
        {
            BlockException.CheckDestroy(this);
            return _inventoryItems[index];
        }
    }
    
    public class BeltConveyorItemJsonObject
    {
        [JsonProperty("itemStack")] public ItemStackSaveJsonObject ItemStack;
        
        [JsonProperty("remainingTime")] public double RemainingPercent;
        
        public BeltConveyorItemJsonObject(BeltConveyorInventoryItem beltConveyorInventoryItem)
        {
            if (beltConveyorInventoryItem == null)
            {
                ItemStack = null;
                RemainingPercent = 1;
                return;
            }
            
            var item = ServerContext.ItemStackFactory.Create(beltConveyorInventoryItem.ItemId, 1);
            ItemStack = new ItemStackSaveJsonObject(item);
            RemainingPercent = beltConveyorInventoryItem.RemainingPercent;
        }
    }
}