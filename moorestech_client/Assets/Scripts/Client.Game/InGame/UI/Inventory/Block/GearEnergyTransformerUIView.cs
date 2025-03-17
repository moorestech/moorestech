using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.StateProcessor;
using Client.Game.InGame.UI.Inventory.Element;
using Core.Item.Interface;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;
using TMPro;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.Block
{
    public class GearEnergyTransformerUIView : MonoBehaviour, IBlockInventoryView
    {
        [SerializeField] private TMP_Text blockNameText;
        [SerializeField] private TMP_Text torque;
        [SerializeField] private TMP_Text rpm;
        
        private BlockGameObject _blockGameObject;
        
        public void Initialize(BlockGameObject blockGameObject)
        {
            blockNameText.text = blockGameObject.BlockMasterElement.Name;
            _blockGameObject = blockGameObject;
        }
        
        private void Update()
        {
            // ここが重かったら検討
            var processor = (GearStateChangeProcessor)_blockGameObject.BlockStateChangeProcessors.FirstOrDefault(x => x as GearStateChangeProcessor);
            if (processor == null)
            {
                Debug.LogError("GearStateChangeProcessorがアタッチされていません。");
                return;
            }
            
            var currentTorque = processor.CurrentGearState?.CurrentTorque ?? 0;
            var currentRpm = processor.CurrentGearState?.CurrentRpm ?? 0;
            
            torque.text = $"トルク: {currentTorque}";
            rpm.text = $"回転数: {currentRpm}";
        }
        
        
        public IReadOnlyList<ItemSlotObject> SubInventorySlotObjects { get; } = new List<ItemSlotObject>();
        public List<IItemStack> SubInventory { get; } = new();
        public int Count => 0;
        public ItemMoveInventoryInfo ItemMoveInventoryInfo { get; } = new(ItemMoveInventoryType.BlockInventory); // インベントリはないので固定値を入れておく
        
        public void UpdateItemList(List<IItemStack> response) { }
        public void UpdateInventorySlot(int slot, IItemStack item) { }
        public void DestroyUI()
        {
            Destroy(gameObject);
        }
    }
}