using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Game.Entity.Interface;
using Game.PlayerConnection;
using Game.PlayerRiding.Interface;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Event;
using Server.Event.EventReceive;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    public class InitialHandshakeProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:initialHandshake";
        
        private readonly IEntitiesDatastore _entitiesDatastore;
        private readonly IEntityFactory _entityFactory;
        private readonly IWorldSettingsDatastore _worldSettingsDatastore;
        private readonly PlayerConnectionRegistry _connectionRegistry;
        private readonly IPlayerRidingDatastore _playerRidingDatastore;
        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly IItemStackLevelLookup _itemStackLevelLookup;

        public InitialHandshakeProtocol(ServiceProvider serviceProvider)
        {
            _itemStackLevelLookup = serviceProvider.GetService<IItemStackLevelLookup>();
            _entitiesDatastore = serviceProvider.GetService<IEntitiesDatastore>();
            _entityFactory = serviceProvider.GetService<IEntityFactory>();
            _worldSettingsDatastore = serviceProvider.GetService<IWorldSettingsDatastore>();
            _connectionRegistry = (PlayerConnectionRegistry)serviceProvider.GetService<IPlayerConnectionChecker>();
            _playerRidingDatastore = serviceProvider.GetService<IPlayerRidingDatastore>();
            _eventProtocolProvider = serviceProvider.GetService<EventProtocolProvider>();
        }
        
        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            var data = MessagePackSerializer.Deserialize<RequestInitialHandshakeMessagePack>(payload);
            _connectionRegistry.Register(data.PlayerId);
            _eventProtocolProvider.RegisterPlayer(data.PlayerId);
            context.BindPlayerId(data.PlayerId);
            
            var response = CreateResponse();
            
            return response;

            #region Internal

            ResponseInitialHandshakeMessagePack CreateResponse()
            {
                // 乗り物に乗っているかどうかの状態の取得
                // Get the riding state if the player is currently riding something.
                RidableIdentifierMessagePack ridingTarget = null;
                var ridingSeatIndex = -1;
                if (_playerRidingDatastore.EvaluateOnLogin(data.PlayerId)
                    && _playerRidingDatastore.TryGetRidingState(data.PlayerId, out var state))
                {
                    ridingTarget = state.Identifier.ToMessagePack();
                    ridingSeatIndex = state.SeatIndex;
                }
                
                var playerPos = GetPlayerPosition(new EntityInstanceId(data.PlayerId));

                // 解放済みスタックレベルを初期データとして同梱する
                // Bundle unlocked stack levels as part of the initial data
                var itemStackLevels = _itemStackLevelLookup.GetUnlockedLevels()
                    .Select(level => new ItemStackLevelUnlockEventPacket.ItemStackLevelMessagePack(level.Key, level.Value))
                    .ToArray();

                return new ResponseInitialHandshakeMessagePack(playerPos, ridingTarget, ridingSeatIndex, itemStackLevels);
            }
            
            Vector3MessagePack GetPlayerPosition(EntityInstanceId playerId)
            {
                if (_entitiesDatastore.Exists(playerId))
                {
                    //プレイヤーがいるのでセーブされた座標を返す
                    var pos = _entitiesDatastore.GetPosition(playerId);
                    return new Vector3MessagePack(pos.x, pos.y, pos.z);
                }
                
                var spawnPoint = _worldSettingsDatastore.WorldSpawnPoint;
                var playerEntity = _entityFactory.CreateEntity(VanillaEntityType.VanillaPlayer, playerId, spawnPoint);
                _entitiesDatastore.Add(playerEntity);
                
                //プレイヤーのデータがなかったのでスポーン地点を取得する
                return new Vector3MessagePack(spawnPoint);
            }

            #endregion
        }
        
        [MessagePackObject]
        public class RequestInitialHandshakeMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public int PlayerId { get; set; }
            [Key(3)] public string PlayerName { get; set; }
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RequestInitialHandshakeMessagePack() { }
            
            public RequestInitialHandshakeMessagePack(int playerId, string playerName)
            {
                Tag = ProtocolTag;
                PlayerId = playerId;
                PlayerName = playerName;
            }
        }
        
        [MessagePackObject]
        public class ResponseInitialHandshakeMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public Vector3MessagePack PlayerPos { get; set; }
            [Key(3)] public InitialHandshakeRidingStateType RidingStateType { get; set; }
            [Key(4)] public RidableIdentifierMessagePack RidingTarget { get; set; }
            [Key(5)] public int RidingSeatIndex { get; set; }
            [Key(6)] public ItemStackLevelUnlockEventPacket.ItemStackLevelMessagePack[] ItemStackLevels { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResponseInitialHandshakeMessagePack() { }

            public ResponseInitialHandshakeMessagePack(
                Vector3MessagePack playerPos,
                RidableIdentifierMessagePack ridingTarget,
                int ridingSeatIndex,
                ItemStackLevelUnlockEventPacket.ItemStackLevelMessagePack[] itemStackLevels)
            {
                Tag = ProtocolTag;
                PlayerPos = playerPos;
                RidingStateType = ridingTarget == null ? InitialHandshakeRidingStateType.None : InitialHandshakeRidingStateType.Restored;
                RidingTarget = ridingTarget;
                RidingSeatIndex = ridingSeatIndex;
                ItemStackLevels = itemStackLevels;
            }

            [IgnoreMember] public bool HasRidingState => RidingStateType == InitialHandshakeRidingStateType.Restored;
        }
    }

    public enum InitialHandshakeRidingStateType : byte
    {
        None,
        Restored,
    }
}
