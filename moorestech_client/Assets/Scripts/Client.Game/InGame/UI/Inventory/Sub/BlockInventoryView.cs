using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Element;
using Core.Item.Interface;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Interface.BlockConfig;
using Game.Context;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;
using TMPro;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.Sub
{
    public class BlockInventoryView : MonoBehaviour, ISubInventory
    {
        [SerializeField] private ItemSlotObject itemSlotObjectPrefab;
        
        #region Generator
        
        [SerializeField] private RectTransform powerGeneratorFuelItemParent;
        
        #endregion
        
        private readonly List<ItemSlotObject> _blockItemSlotObjects = new();
        public IReadOnlyList<ItemSlotObject> SubInventorySlotObjects => _blockItemSlotObjects;
        public List<IItemStack> SubInventory { get; private set; }
        public int Count => _blockItemSlotObjects.Count;
        public ItemMoveInventoryInfo ItemMoveInventoryInfo { get; private set; }
        
        public void SetActive(bool isActive)
        {
            gameObject.SetActive(isActive);
        }
        
        public void SetBlockInventoryType(BlockInventoryType type, Vector3Int blockPos, IBlockConfigParam param, int blockId)
        {
            var itemStackFactory = ServerContext.ItemStackFactory;
            ItemMoveInventoryInfo = new ItemMoveInventoryInfo(ItemMoveInventoryType.BlockInventory, blockPos);
            
            Clear();
            
            switch (type)
            {
                case BlockInventoryType.Chest:
                    Chest();
                    break;
                case BlockInventoryType.Miner:
                    Miner();
                    break;
                case BlockInventoryType.Machine:
                    Machine();
                    break;
                case BlockInventoryType.Generator:
                    Generator();
                    break;
            }
            
            #region Internal
            
            void Clear()
            {
                foreach (var slotObject in _blockItemSlotObjects) Destroy(slotObject.gameObject);
                _blockItemSlotObjects.Clear();
                
                chestItemParent.gameObject.SetActive(false);
                minerItemParent.gameObject.SetActive(false);
                machineUIParent.SetActive(false);
            }
            
            void Chest()
            {
                chestItemParent.gameObject.SetActive(true);
                
                var itemList = new List<IItemStack>();
                var chestParam = (ChestConfigParam)param;
                for (var i = 0; i < chestParam.ChestItemNum; i++)
                {
                    var slotObject = Instantiate(itemSlotObjectPrefab, chestSlotsParent);
                    _blockItemSlotObjects.Add(slotObject);
                    itemList.Add(itemStackFactory.CreatEmpty());
                }
                
                SetItemList(itemList);
            }
            
            void Miner()
            {
                minerItemParent.gameObject.SetActive(true);
                
                var itemList = new List<IItemStack>();
                var minerParam = (MinerBlockConfigParam)param;
                
                for (var i = 0; i < minerParam.OutputSlot; i++)
                {
                    var slotObject = Instantiate(itemSlotObjectPrefab, minerResultsParent);
                    _blockItemSlotObjects.Add(slotObject);
                    itemList.Add(itemStackFactory.CreatEmpty());
                }
                
                SetItemList(itemList);
                
                var outputImage = ClientContext.ItemImageContainer.GetItemView(minerParam.OutputSlot);
                minerResourceSlot.SetItem(outputImage, 0);
            }
            
            void Machine()
            {
                machineUIParent.gameObject.SetActive(true);
                var itemList = new List<IItemStack>();
                var machineParam = (MachineBlockConfigParam)param;
                for (var i = 0; i < machineParam.InputSlot; i++)
                {
                    var slotObject = Instantiate(itemSlotObjectPrefab, machineInputItemParent);
                    _blockItemSlotObjects.Add(slotObject);
                    itemList.Add(itemStackFactory.CreatEmpty());
                }
                
                for (var i = 0; i < machineParam.OutputSlot; i++)
                {
                    var slotObject = Instantiate(itemSlotObjectPrefab, machineOutputItemParent);
                    _blockItemSlotObjects.Add(slotObject);
                    itemList.Add(itemStackFactory.CreatEmpty());
                }
                
                var config = ServerContext.BlockConfig.GetBlockConfig(blockId);
                machineBlockNameText.text = config.Name;
                SetItemList(itemList);
            }
            
            void Generator()
            {
                powerGeneratorFuelItemParent.gameObject.SetActive(true);
                
                var itemList = new List<IItemStack>();
                var generatorParam = (PowerGeneratorConfigParam)param;
                for (var i = 0; i < generatorParam.FuelSlot; i++)
                {
                    var slotObject = Instantiate(itemSlotObjectPrefab, powerGeneratorFuelItemParent);
                    _blockItemSlotObjects.Add(slotObject);
                    itemList.Add(itemStackFactory.CreatEmpty());
                }
                
                SetItemList(itemList);
            }
            
            #endregion
        }
        
        public void SetItemList(List<IItemStack> itemStacks)
        {
            SubInventory = itemStacks;
        }
        
        public void SetItemSlot(int slot, IItemStack item)
        {
            if (SubInventory.Count <= slot)
            {
                Debug.LogError($"インベントリのサイズを超えています。item:{item} slot:{slot}");
                return;
            }
            
            SubInventory[slot] = item;
        }
        
        
        #region Chest
        
        [SerializeField] private RectTransform chestItemParent;
        [SerializeField] private RectTransform chestSlotsParent;
        
        #endregion
        
        #region Miner
        
        [SerializeField] private RectTransform minerItemParent;
        [SerializeField] private ItemSlotObject minerResourceSlot;
        [SerializeField] private RectTransform minerResultsParent;
        
        #endregion
        
        #region Machine
        
        [SerializeField] private GameObject machineUIParent;
        
        [SerializeField] private RectTransform machineInputItemParent;
        [SerializeField] private RectTransform machineOutputItemParent;
        [SerializeField] private TMP_Text machineBlockNameText;
        
        #endregion
    }
    
    public enum BlockInventoryType
    {
        Chest,
        Miner,
        Machine,
        Generator,
    }
}