using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.UI.Inventory.Common;
using Core.Item.Interface;
using Game.Gear.Common;
using Game.PlayerInventory.Interface.Subscription;
using TMPro;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.Block
{
    public class GearEnergyTransformerUIView : MonoBehaviour, IBlockInventoryView
    {
        [SerializeField] private TMP_Text blockNameText;
        [SerializeField] private TMP_Text torque;
        [SerializeField] private TMP_Text rpm;
        [SerializeField] private TMP_Text networkInfo;
        
        private BlockGameObject _blockGameObject;
        
        public void Initialize(BlockGameObject blockGameObject)
        {
            blockNameText.text = blockGameObject.BlockMasterElement.Name;
            _blockGameObject = blockGameObject;
        }
        
        private void Update()
        {
            var state = _blockGameObject.GetStateDetail<GearStateDetail>(GearStateDetail.BlockStateDetailKey);
            if (state == null)
            {
                Debug.LogError("Failed to get CommonMachineBlockStateDetail.");
                return;
            }
            
            var currentTorque = state.CurrentTorque;
            var currentRpm = state.CurrentRpm;
            
            torque.text = $"Torque: {currentTorque}";
            rpm.text = $"RPM: {currentRpm}";
            
            var rate = state.GearNetworkOperatingRate;
            var requiredPower = state.GearNetworkTotalRequiredPower;
            var generatePower = state.GearNetworkTotalGeneratePower;
            networkInfo.text = $"{GetStopReasonText(state.StopReason)} Required power: {requiredPower:F2} Generated power: {generatePower:F2}";
        }
        
        public static string GetStopReasonText(GearNetworkStopReason reason)
        {
            var text = reason switch
            {
                GearNetworkStopReason.None => string.Empty,
                GearNetworkStopReason.OverRequirePower => "Insufficient power",
                GearNetworkStopReason.Rocked => "Locked",
                _ => string.Empty
            };
            
            return text == string.Empty ? string.Empty : $"<color=red>{text} </color>";
        }
        
        
        public IReadOnlyList<ItemSlotView> SubInventorySlotObjects { get; } = new List<ItemSlotView>();
        public List<IItemStack> SubInventory { get; } = new();
        public int Count => 0;
        public ISubInventoryIdentifier ISubInventoryIdentifier { get; } = null; // インベントリはないのでnullを入れておく
        
        public void UpdateItemList(List<IItemStack> response) { }
        public void UpdateInventorySlot(int slot, IItemStack item) { }
        public void DestroyUI()
        {
            Destroy(gameObject);
        }
    }
}