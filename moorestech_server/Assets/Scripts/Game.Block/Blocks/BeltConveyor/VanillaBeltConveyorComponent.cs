using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.BeltConveyor.Connector;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
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
        private readonly IBeltConveyorInventoryItem[] _inventoryItems;
        
        private readonly IBeltConveyorItemFactory _beltConveyorItemFactory;
        private readonly IBeltConveyorConnector _beltConveyorConnector;
        private readonly int _inventoryItemNum;
        
        private double _timeOfItemEnterToExit; //ベルトコンベアにアイテムが入って出るまでの時間
        
        public VanillaBeltConveyorComponent(int inventoryItemNum, float timeOfItemEnterToExit, IBeltConveyorConnector beltConveyorConnector, BeltConveyorSlopeType slopeType, IBeltConveyorItemFactory beltConveyorItemFactory)
        {
            SlopeType = slopeType;
            _inventoryItemNum = inventoryItemNum;
            _timeOfItemEnterToExit = timeOfItemEnterToExit;
            _beltConveyorItemFactory = beltConveyorItemFactory;
            _beltConveyorConnector = beltConveyorConnector;
            
            _inventoryItems = new IBeltConveyorInventoryItem[inventoryItemNum];
        }
        
        public VanillaBeltConveyorComponent(string state, int inventoryItemNum, float timeOfItemEnterToExit, IBeltConveyorConnector beltConveyorConnector, BeltConveyorSlopeType slopeType, IBeltConveyorItemFactory beltConveyorItemFactory) :
            this(inventoryItemNum, timeOfItemEnterToExit, beltConveyorConnector, slopeType, beltConveyorItemFactory)
        {
            //stateから復元
            //データがないときは何もしない
            if (state == string.Empty) return;
            
            List<string> itemJsons = JsonConvert.DeserializeObject<List<string>>(state);
            for (var i = 0; i < itemJsons.Count; i++)
            {
                if (itemJsons[i] != null)
                {
                    _inventoryItems[i] = _beltConveyorItemFactory.LoadItem(itemJsons[i]);
                }
            }
        }
        
        public IItemStack InsertItem(IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);
            
            //新しく挿入可能か
            if (_inventoryItems[^1] != null)
                //挿入可能でない
                return itemStack;
            
            _inventoryItems[^1] = _beltConveyorItemFactory.CreateItem(itemStack.Id, itemStack.ItemInstanceId);
            
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
            _inventoryItems[slot] = new VanillaBeltConveyorInventoryItem(itemStack.Id, itemStack.ItemInstanceId);
        }
        
        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
        
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
                
                //次のインデックスに入れる時間かどうかをチェックする
                var nextIndexStartTime = i * (1f / _inventoryItemNum);
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
                    var insertItem = ServerContext.ItemStackFactory.Create(item.ItemId, 1, item.ItemInstanceId);
                    
                    var output = _beltConveyorConnector.InsertItem(insertItem);
                    
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
    }
}