using System.Collections.Generic;
using Game.Context;
using Game.PlayerInventory.Event;
using Game.PlayerInventory.Interface;
using Game.PlayerInventory.Interface.Event;
using Game.PlayerInventory.ItemManaged;
using UniRx;
using UnityEngine;

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

        // 初期装備の配布が決着済みのプレイヤー。付与済みとセーブ復元済みの両方が入る
        // Players whose initial equipment is settled: both the granted ones and those restored from a save
        private readonly HashSet<int> _initialEquipmentSettledPlayerIds = new();
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
            if (!_playerInventoryData.ContainsKey(playerId)) _playerInventoryData.Add(playerId, CreateInventoryData(playerId));

            return _playerInventoryData[playerId];
        }

        /// <summary>
        ///     接続確定時に呼ばれ、初期装備をまだ受け取っていないプレイヤーにだけ1度だけ配る
        ///     Called when a connection is established; grants the initial equipment exactly once, only to a player who has not received it
        /// </summary>
        public void GrantInitialEquipmentIfNewPlayer(int playerId)
        {
            // 配布の可否は「決着済みか」だけで決める。取得で先に空インベントリが作られていても新規のまま
            // Only the settled set decides; a player is still new even when a fetch already built an empty inventory
            if (!_initialEquipmentSettledPlayerIds.Add(playerId)) return;

            // 生成元がこのクラスだけなので装備は必ずこの実装。無イベント投入は復元と同じ経路を使う
            // This class is the only creator, so the equipment is always this implementation; the event-free path is shared with restore
            var equipment = (EquipmentInventoryData)GetInventoryData(playerId).EquipmentInventory;

            // 接続前にイベントを飛ばさないため無イベント投入で入れる
            // Insert without events so nothing is sent before the client is listening
            var initialStacks = InitialEquipmentMasterUtil.CreateInitialEquipmentStacks(ServerContext.ItemStackFactory);
            var overflowInitialItems = equipment.SetItemsWithoutEvent(initialStacks);
            foreach (var overflow in overflowInitialItems)
            {
                if (overflow.Count == 0) continue;
                Debug.LogError($"初期装備が装備スロットに収まりません playerId:{playerId} itemId:{overflow.Id} count:{overflow.Count}");
            }
        }

        private PlayerInventoryData CreateInventoryData(int playerId)
        {
            var main = new MainOpenableInventoryData(playerId, _mainInventoryUpdateEvent, _slotLevelDataStore.CurrentSlotCount);
            var grab = new GrabInventoryData(playerId, _grabInventoryUpdateEvent);
            var equipment = new EquipmentInventoryData(playerId, _equipmentInventoryUpdateEvent);
            return new PlayerInventoryData(main, grab, equipment);
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
                
                //アイテムを復元
                // 装備を先に復元し、マスタのスロット数が縮んであふれた分を受け取る
                // Restore equipment first and take the overflow caused by a shrunk master slot count
                var equipment = new EquipmentInventoryData(playerId, _equipmentInventoryUpdateEvent);
                var overflowEquipmentItems = equipment.RestoreFromSave(equipmentItems, selectedEquipmentIndex);

                // セーブ済みアイテム数と装備あふれ分が収まるまでスロットを拡張し、アイテムを絶対に消さない
                // Expand slots until both the saved items and the equipment overflow fit so no item is ever lost
                var slotCount = System.Math.Max(_slotLevelDataStore.CurrentSlotCount, mainItems.Count + overflowEquipmentItems.Count);
                var main = new MainOpenableInventoryData(playerId, _mainInventoryUpdateEvent, slotCount, mainItems);
                var notInsertedItems = main.InsertItem(overflowEquipmentItems);

                // 枠は算術上必ず足りるため、入り切らない分が出た時点でスロット数計算のバグを意味する
                // The slot math always leaves room, so any leftover means the slot count calculation is broken
                foreach (var notInserted in notInsertedItems)
                {
                    if (notInserted.Count == 0) continue;
                    Debug.LogError($"装備あふれ分をメインインベントリへ退避できませんでした playerId:{playerId} itemId:{notInserted.Id} count:{notInserted.Count}");
                }

                var grab = new GrabInventoryData(playerId, _grabInventoryUpdateEvent, grabItem);

                var playerInventory = new PlayerInventoryData(main, grab, equipment);
                
                //インベントリの追加を行う　既にあるなら置き換える
                _playerInventoryData[playerId] = playerInventory;

                // 復元済みプレイヤーへ初期装備を配ると、捨てた装備が接続のたびに復活する
                // Granting to a restored player would resurrect discarded equipment on every connection
                _initialEquipmentSettledPlayerIds.Add(playerId);
            }
        }
    }
}