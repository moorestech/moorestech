using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Newtonsoft.Json;

namespace Game.PlayerInventory.Interface
{
    public class PlayerInventorySaveJsonObject
    {
        [JsonProperty("PlayerId")] public int PlayerId;
        
        [JsonProperty("MainInventoryItems")] public List<ItemStackSaveJsonObject> MainInventoryItems;
        
        [JsonProperty("GrabInventoryItems")] public ItemStackSaveJsonObject GrabInventoryItem;

        // 装備スロット数はマスタ定義なので保存せず、中身と選択位置だけを保存する
        // The equipment slot count is master data, so only the contents and the selection are saved
        [JsonProperty("EquipmentInventoryItems")] public List<ItemStackSaveJsonObject> EquipmentInventoryItems;

        [JsonProperty("SelectedEquipmentIndex")] public int SelectedEquipmentIndex;

        public PlayerInventorySaveJsonObject()
        {
        }
        
        public PlayerInventorySaveJsonObject(int playerId, PlayerInventoryData playerInventoryData)
        {
            MainInventoryItems = new List<ItemStackSaveJsonObject>();
            for (var i = 0; i < playerInventoryData.MainOpenableInventory.GetSlotSize(); i++)
            {
                var item = playerInventoryData.MainOpenableInventory.GetItem(i);
                MainInventoryItems.Add(new ItemStackSaveJsonObject(item));
            }
            
            var grabItemStack = playerInventoryData.GrabInventory.GetItem(0);
            GrabInventoryItem = new ItemStackSaveJsonObject(grabItemStack);

            EquipmentInventoryItems = new List<ItemStackSaveJsonObject>();
            for (var i = 0; i < playerInventoryData.EquipmentInventory.GetSlotSize(); i++)
            {
                var item = playerInventoryData.EquipmentInventory.GetItem(i);
                EquipmentInventoryItems.Add(new ItemStackSaveJsonObject(item));
            }
            SelectedEquipmentIndex = playerInventoryData.EquipmentInventory.SelectedEquipmentIndex;

            PlayerId = playerId;
        }

        public (List<IItemStack> mainInventory, IItemStack grabItem, List<IItemStack> equipmentItems, int selectedEquipmentIndex) GetPlayerInventoryData()
        {
            var mainItemStack = new List<IItemStack>();
            foreach (var items in MainInventoryItems)
            {
                mainItemStack.Add(items.ToItemStack());
            }
            var grabItem = GrabInventoryItem.ToItemStack();

            // 装備フィールドの無い旧セーブは空装備として扱う
            // Legacy saves without the equipment fields start with empty equipment
            var equipmentItems = new List<IItemStack>();
            if (EquipmentInventoryItems != null)
            {
                foreach (var items in EquipmentInventoryItems)
                {
                    equipmentItems.Add(items.ToItemStack());
                }
            }

            return (mainItemStack, grabItem, equipmentItems, SelectedEquipmentIndex);
        }
    }
}