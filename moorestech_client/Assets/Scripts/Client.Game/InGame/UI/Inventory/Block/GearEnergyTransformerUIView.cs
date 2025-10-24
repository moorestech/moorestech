using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.UI.Inventory.Common;
using Core.Item.Interface;
using Game.Gear.Common;
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
                Debug.LogError("CommonMachineBlockStateDetailが取得できません。");
                return;
            }
            
            var currentTorque = state.CurrentTorque;
            var currentRpm = state.CurrentRpm;
            
            torque.text = $"トルク: {currentTorque}";
            rpm.text = $"回転数: {currentRpm}";
            
            var rate = state.GearNetworkOperatingRate;
            var requiredPower = state.GearNetworkTotalRequiredPower;
            var generatePower = state.GearNetworkTotalGeneratePower;
            networkInfo.text = $"{GetStopReasonText(state.StopReason)} 必要力: {requiredPower:F2} 生成力: {generatePower:F2}";
        }
        
        public static string GetStopReasonText(GearNetworkStopReason reason)
        {
            var text = reason switch
            {
                GearNetworkStopReason.None => string.Empty,
                GearNetworkStopReason.OverRequirePower => "パワー不足",
                GearNetworkStopReason.Rocked => "ロック",
                _ => string.Empty
            };
            
            return text == string.Empty ? string.Empty : $"<color=red>{text} </color>";
        }
        
        
        public IReadOnlyList<ItemSlotView> SubInventorySlotObjects { get; } = new List<ItemSlotView>();
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