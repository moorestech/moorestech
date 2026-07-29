using System.Collections.Generic;
using Game.PlayerInventory.Event;
using Game.PlayerInventory.Interface;
using Game.PlayerInventory.Interface.Event;
using Game.PlayerInventory.ItemManaged;
using UniRx;

namespace Game.PlayerInventory
{
    /// <summary>
    ///     プレイヤーインベントリのデータを扱います。
    ///     TODO プレイヤーのエンティティ内で管理すべきか検討中
    /// </summary>
    public class PlayerInventoryDataStore : IPlayerInventoryDataStore
    {
        private readonly EquipmentInventoryUpdateEvent _equipmentInventoryUpdateEvent;
        private readonly GrabInventoryUpdateEvent _grabInventoryUpdateEvent;


        private readonly MainInventoryUpdateEvent _mainInventoryUpdateEvent;
        private readonly Dictionary<int, PlayerInventoryData> _playerInventoryData = new();
        private readonly IPlayerInventorySlotLevelDataStore _slotLevelDataStore;

        public PlayerInventoryDataStore(IMainInventoryUpdateEvent mainInventoryUpdateEvent, IGrabInventoryUpdateEvent grabInventoryUpdateEvent, IEquipmentInventoryUpdateEvent equipmentInventoryUpdateEvent, IPlayerInventorySlotLevelDataStore slotLevelDataStore)
        {
            //イベントの呼び出しをアセンブリに隠蔽するため、インターフェースをキャストします。
            _mainInventoryUpdateEvent = (MainInventoryUpdateEvent)mainInventoryUpdateEvent;
            _grabInventoryUpdateEvent = (GrabInventoryUpdateEvent)grabInventoryUpdateEvent;
            _equipmentInventoryUpdateEvent = (EquipmentInventoryUpdateEvent)equipmentInventoryUpdateEvent;
            _slotLevelDataStore = slotLevelDataStore;

            // レベル上昇で全プレイヤー拡張
            // Expand all players on level up
            _slotLevelDataStore.OnSlotCountChanged.Subscribe(slotCount =>
            {
                foreach (var inventory in _playerInventoryData.Values)
                    ((MainOpenableInventoryData)inventory.MainOpenableInventory).ExpandSlots(slotCount);
            });
        }
        
        public List<int> GetAllPlayerId()
        {
            return new List<int>(_playerInventoryData.Keys);
        }
        
        public PlayerInventoryData GetInventoryData(int playerId)
        {
            if (!_playerInventoryData.ContainsKey(playerId))
            {
                var main = new MainOpenableInventoryData(playerId, _mainInventoryUpdateEvent, _slotLevelDataStore.CurrentSlotCount);
                var grab = new GrabInventoryData(playerId, _grabInventoryUpdateEvent);
                var equipment = new EquipmentInventoryData(playerId, _equipmentInventoryUpdateEvent);

                _playerInventoryData.Add(playerId, new PlayerInventoryData(main, grab, equipment));
            }
            
            return _playerInventoryData[playerId];
        }
        
        public List<PlayerInventorySaveJsonObject> GetSaveJsonObject()
        {
            var savePlayerInventoryList = new List<PlayerInventorySaveJsonObject>();
            //セーブデータに必要なデータをまとめる
            foreach (var inventory in _playerInventoryData)
            {
                var saveInventoryData = new PlayerInventorySaveJsonObject(inventory.Key, inventory.Value);
                savePlayerInventoryList.Add(saveInventoryData);
            }
            
            return savePlayerInventoryList;
        }
        
        /// <summary>
        ///     プレイヤーのデータを置き換える
        /// </summary>
        public void LoadPlayerInventory(List<PlayerInventorySaveJsonObject> saveInventoryDataList)
        {
            foreach (var saveInventory in saveInventoryDataList)
            {
                var playerId = saveInventory.PlayerId;
                (var mainItems, var grabItem, var equipmentItems, var selectedEquipmentIndex) = saveInventory.GetPlayerInventoryData();
                
                // メインのスロット数を決める前に、装備できず退避される分を先に確定させる
                // Settle the items rejected by equipment before deciding main's slot count
                var equipment = new EquipmentInventoryData(playerId, _equipmentInventoryUpdateEvent);
                var rejectedEquipmentItems = equipment.RestoreFromSave(equipmentItems, selectedEquipmentIndex);

                //アイテムを復元
                // セーブ済みアイテムと退避分が必ず収まるまでスロット数を拡張し、アイテムを消さない
                // Expand the slot count until saved items and the fallback always fit, so nothing is destroyed
                var slotCount = System.Math.Max(_slotLevelDataStore.CurrentSlotCount, mainItems.Count + rejectedEquipmentItems.Count);
                var main = new MainOpenableInventoryData(playerId, _mainInventoryUpdateEvent, slotCount, mainItems);
                var grab = new GrabInventoryData(playerId, _grabInventoryUpdateEvent, grabItem);

                // 1スタックは必ず1スロットに収まるため、退避分の空きは上のスロット数確保で保証される
                // A stack never exceeds one slot, so the reservation above guarantees room for every rejected item
                main.InsertItem(rejectedEquipmentItems);

                var playerInventory = new PlayerInventoryData(main, grab, equipment);
                
                //インベントリの追加を行う　既にあるなら置き換える
                _playerInventoryData[playerId] = playerInventory;
            }
        }
    }
}