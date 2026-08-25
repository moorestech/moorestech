using System;
using System.Collections.Generic;
using Client.Game.InGame.Control;
using Client.Game.InGame.Player;
using Client.Game.InGame.UI.Inventory.Equipment;
using Client.Network.API;
using Core.Item.Interface;
using Core.Master;
using Game.Hotbar;
using Game.MapGeneration.Transfer;
using Game.PlayerInventory.Interface;
using Game.Research;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.MapData;
using Server.Util.MessagePack;
using static Server.Protocol.PacketResponse.GetMapObjectInfoProtocol;
using UnityEngine;

namespace Client.DebugSystem
{
    public class CharacterTestDebug : MonoBehaviour
    {
        [SerializeField] private InGameCameraController _cameraController;
        [SerializeField] private PlayerSystemContainer _playerSystemContainer;
        
        private void Start()
        {
            // テスト用の初期デバッグデータを生成
            // Generate initial debug data for testing
            var initialHandshakeResponse = CreateInitialHandshakeResponse();
            _playerSystemContainer.Construct(initialHandshakeResponse);

            // デバッグシーンの地形はシーン配置済みなので、Finalizerと同じ明示プッシュをその場で行う
            // The debug scene authors its terrain in the scene, so the finalizer's explicit push happens right here
            _playerSystemContainer.StartPlayerRuntime();
            _cameraController.SetControllable(true);
            
            #region Internal
            
            InitialHandshakeResponse CreateInitialHandshakeResponse()
            {
                // プレイヤーの初期位置をコンテナから取得
                // Fetch initial player position from the container
                var playerPos = _playerSystemContainer.transform.position;
                var handshake = new InitialHandshakeProtocol.ResponseInitialHandshakeMessagePack(new Vector3MessagePack(playerPos), null, -1, Array.Empty<ItemStackLevelUnlockEventPacket.ItemStackLevelMessagePack>(), new Guid[HotbarAssignmentDatastore.SlotCount], Array.Empty<RemainingPlacementCountChangedEventPacket.RemainingPlacementCountMessagePack>());
                var worldData = new WorldDataResponse(new List<BlockInfo>(), new List<EntityResponse>());
                var emptyItem = new ItemMessagePack(ItemMaster.EmptyItemId, 0);
                var inventory = new PlayerInventoryResponse(new PlayerInventoryResponseProtocol.PlayerInventoryResponseProtocolMessagePack(
                    0, Array.Empty<ItemMessagePack>(), emptyItem, Array.Empty<ItemMessagePack>(), IEquipmentInventory.BareHandsIndex));
                var unlockState = new UnlockStateResponse(
                    lockedCraftRecipeGuids: new List<Guid>(),
                    unlockedCraftRecipeGuids: new List<Guid>(),
                    lockedItemIds: new List<ItemId>(),
                    unlockedItemIds: new List<ItemId>(),
                    lockedChallengeCategoryGuids: new List<Guid>(),
                    unlockedChallengeCategoryGuids: new List<Guid>(),
                    lockedMachineRecipeGuids: new List<Guid>(),
                    unlockedMachineRecipeGuids: new List<Guid>(),
                    lockedBlockGuids: new List<Guid>(),
                    unlockedBlockGuids: new List<Guid>(),
                    lockedTrainCarGuids: new List<Guid>(),
                    unlockedTrainCarGuids: new List<Guid>(),
                    lockedConnectToolGuids: new List<Guid>(),
                    unlockedConnectToolGuids: new List<Guid>(),
                    isBlueprintUnlocked: false);
                
                // テストプレイ用の空レスポンスを構築
                // Build an empty response set for test play
                var mapObjects = new List<MapObjectsInfoMessagePack>();
                var challenges = new List<ChallengeCategoryResponse>();
                var playedSkitIds = new List<string>();
                var researchNodeStates = new Dictionary<Guid, ResearchNodeState>();
                var mapLayout = new GetMapDataProtocol.ResponseMapDataMessagePack(new Vector3MessagePack(Vector3.zero), new List<MapObjectLayoutMessagePack>(), new List<VeinLayoutMessagePack>(), TerrainTransferMeta.CreateWithoutWorldDirectory(), string.Empty);

                var responses = (
                    mapObjects,
                    worldData,
                    inventory,
                    challenges,
                    unlockState,
                    playedSkitIds,
                    researchNodeStates,
                    mapLayout);
                
                return new InitialHandshakeResponse(handshake, responses);
            }
            
            #endregion
        }
    }
}
