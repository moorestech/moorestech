using System;
using System.Collections.Generic;
using Client.Game.InGame.Control;
using Client.Game.InGame.Player;
using Client.Network.API;
using Core.Item.Interface;
using Core.Master;
using Game.CraftTree.Models;
using Game.Research;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse;
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
            _cameraController.SetControllable(true);
            
            #region Internal
            
            InitialHandshakeResponse CreateInitialHandshakeResponse()
            {
                // プレイヤーの初期位置をコンテナから取得
                // Fetch initial player position from the container
                var playerPos = _playerSystemContainer.transform.position;
                var handshake = new InitialHandshakeProtocol.ResponseInitialHandshakeMessagePack(new Vector3MessagePack(playerPos));
                var worldData = new WorldDataResponse(new List<BlockInfo>(), new List<EntityResponse>());
                var inventory = new PlayerInventoryResponse(new List<IItemStack>(), null);
                var unlockState = new UnlockStateResponse(new List<Guid>(), new List<Guid>(), new List<ItemId>(), new List<ItemId>(), new List<Guid>(), new List<Guid>());
                var craftTree = new CraftTreeResponse(new List<CraftTreeNode>(), Guid.Empty);
                
                // テストプレイ用の空レスポンスを構築
                // Build an empty response set for test play
                var mapObjects = new List<MapObjectsInfoMessagePack>();
                var challenges = new List<ChallengeCategoryResponse>();
                var playedSkitIds = new List<string>();
                var researchNodeStates = new Dictionary<Guid, ResearchNodeState>();
                var railGraphSnapshot = new RailGraphSnapshotMessagePack
                {
                    Nodes = new List<RailNodeCreatedMessagePack>(),
                    Connections = new List<RailGraphConnectionSnapshotMessagePack>(),
                    GraphHash = 0u,
                    GraphTick = 0,
                };
                var trainUnitSnapshots = new TrainUnitSnapshotResponse(new List<TrainUnitSnapshotBundleMessagePack>(), 0);
                
                var responses = (
                    mapObjects,
                    worldData,
                    inventory,
                    challenges,
                    unlockState,
                    craftTree,
                    playedSkitIds,
                    researchNodeStates,
                    railGraphSnapshot,
                    trainUnitSnapshots);
                
                return new InitialHandshakeResponse(handshake, responses);
            }
            
            #endregion
        }
    }
}
