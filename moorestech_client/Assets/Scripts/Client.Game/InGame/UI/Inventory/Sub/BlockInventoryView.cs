using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.StateProcessor;
using Client.Game.InGame.UI.Inventory.Element;
using Core.Item.Interface;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;
using TMPro;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.Sub
{
    /// <summary>
    /// TODO このへんもユーザー拡張できるように整理する
    /// TODO いい感じに分割する
    /// </summary>
    public class BlockInventoryView : MonoBehaviour, ISubInventory
    {
        [SerializeField] private ItemSlotObject itemSlotObjectPrefab;
        
        #region Chest
        
        [SerializeField] private RectTransform chestItemParent;
        [SerializeField] private RectTransform chestSlotsParent;
        
        #endregion
        
        #region Miner
        
        [SerializeField] private RectTransform minerItemParent;
        [SerializeField] private ItemSlotObject minerResourceSlot;
        [SerializeField] private RectTransform minerResultsParent;
        
        [SerializeField] private ProgressArrowView minerProgressArrow;
        
        #endregion
        
        #region Machine
        
        [SerializeField] private GameObject machineUIParent;
        
        [SerializeField] private RectTransform machineInputItemParent;
        [SerializeField] private RectTransform machineOutputItemParent;
        [SerializeField] private TMP_Text machineBlockNameText;
        
        [SerializeField] private ProgressArrowView machineProgressArrow;
        
        #endregion
        
        #region Generator
        
        [SerializeField] private RectTransform powerGeneratorFuelItemParent;
        
        #endregion
        
        public IReadOnlyList<ItemSlotObject> SubInventorySlotObjects => _blockItemSlotObjects;
        public int Count => _blockItemSlotObjects.Count;
        private readonly List<ItemSlotObject> _blockItemSlotObjects = new();
        
        public List<IItemStack> SubInventory { get; private set; } = new();
        public ItemMoveInventoryInfo ItemMoveInventoryInfo { get; private set; }
        
        private BlockGameObject _currentBlockGameObject;
        private BlockInventoryType _currentBlockInventoryType;
        
        public void OpenBlockInventoryType(BlockInventoryType type, BlockGameObject blockGameObject)
        {
            _currentBlockGameObject = blockGameObject;
            _currentBlockInventoryType = type;
            var itemStackFactory = ServerContext.ItemStackFactory;
            var param = blockGameObject.BlockMasterElement.BlockParam;
            ItemMoveInventoryInfo = new ItemMoveInventoryInfo(ItemMoveInventoryType.BlockInventory, blockGameObject.BlockPosInfo.OriginalPos);
            
            Clear();
            gameObject.SetActive(true);
            
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
                var chestParam = (ChestBlockParam)param;
                for (var i = 0; i < chestParam.ChestItemSlotCount; i++)
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
                var outputCount = param switch
                {
                    ElectricMinerBlockParam blockParam => blockParam.OutputItemSlotCount, // TODO ブロックインベントリの整理箇所
                    GearMinerBlockParam blockParam => blockParam.OutputItemSlotCount,
                    _ => 0
                };
                
                for (var i = 0; i < outputCount; i++)
                {
                    var slotObject = Instantiate(itemSlotObjectPrefab, minerResultsParent);
                    _blockItemSlotObjects.Add(slotObject);
                    itemList.Add(itemStackFactory.CreatEmpty());
                }
                
                SetItemList(itemList);
            }
            
            void Machine()
            {
                machineUIParent.gameObject.SetActive(true);
                var itemList = new List<IItemStack>();
                var inputCount = param switch
                {
                    ElectricMachineBlockParam blockParam => blockParam.InputItemSlotCount, // TODO ブロックインベントリの整理箇所
                    GearMachineBlockParam blockParam => blockParam.InputItemSlotCount,
                    _ => 0
                };
                var outputCount = param switch
                {
                    ElectricMachineBlockParam blockParam => blockParam.OutputItemSlotCount, // TODO ブロックインベントリの整理箇所
                    GearMachineBlockParam blockParam => blockParam.OutputItemSlotCount,
                    _ => 0
                };
                
                for (var i = 0; i < inputCount; i++)
                {
                    var slotObject = Instantiate(itemSlotObjectPrefab, machineInputItemParent);
                    _blockItemSlotObjects.Add(slotObject);
                    itemList.Add(itemStackFactory.CreatEmpty());
                }
                
                for (var i = 0; i < outputCount; i++)
                {
                    var slotObject = Instantiate(itemSlotObjectPrefab, machineOutputItemParent);
                    _blockItemSlotObjects.Add(slotObject);
                    itemList.Add(itemStackFactory.CreatEmpty());
                }
                
                machineBlockNameText.text = blockGameObject.BlockMasterElement.Name;
                SetItemList(itemList);
            }
            
            void Generator()
            {
                powerGeneratorFuelItemParent.gameObject.SetActive(true);
                
                var itemList = new List<IItemStack>();
                var generatorParam = (ElectricGeneratorBlockParam)param;
                for (var i = 0; i < generatorParam.FuelItemSlotCount; i++)
                {
                    var slotObject = Instantiate(itemSlotObjectPrefab, powerGeneratorFuelItemParent);
                    _blockItemSlotObjects.Add(slotObject);
                    itemList.Add(itemStackFactory.CreatEmpty());
                }
                
                SetItemList(itemList);
            }
            
            #endregion
        }
        
        public void CloseBlockInventory()
        {
            gameObject.SetActive(false);
            _currentBlockGameObject = null;
        }
        
        public void SetItemList(List<IItemStack> itemStacks)
        {
            SubInventory = itemStacks;
        }
        
        public void UpdateInventorySlot(int slot, IItemStack item)
        {
            if (SubInventory.Count <= slot)
            {
                Debug.LogError($"インベントリのサイズを超えています。item:{item} slot:{slot}");
                return;
            }
            
            SubInventory[slot] = item;
        }
        
        private void Update()
        {
            // ブロックの登録がなければ処理を終了
            if (_currentBlockGameObject == null) return;
            
            switch (_currentBlockInventoryType)
            {
                case BlockInventoryType.Miner or BlockInventoryType.Machine:
                    CommonMachineUpdate();
                    break;
            }
            
            
        }
        private void CommonMachineUpdate()
        {
            // ここが重かったら検討
            var commonProcessor = (CommonMachineBlockStateChangeProcessor)_currentBlockGameObject.BlockStateChangeProcessors.FirstOrDefault(x => x as CommonMachineBlockStateChangeProcessor); 
            if (commonProcessor == null) return;
            var progressArrow = _currentBlockInventoryType == BlockInventoryType.Miner ? minerProgressArrow : machineProgressArrow;
            progressArrow.SetProgress(commonProcessor.CurrentMachineState.processingRate);
        }
    }
    
    public enum BlockInventoryType
    {
        Chest,
        Miner,
        Machine,
        Generator,
    }
}