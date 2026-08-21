using System;
using System.Collections.Generic;
using Game.PlayerRiding.Interface;
using Game.Research;
using Server.Event.EventReceive;
using UnityEngine;
using static Server.Protocol.PacketResponse.GetMapDataProtocol;
using static Server.Protocol.PacketResponse.GetMapObjectInfoProtocol;
using static Server.Protocol.PacketResponse.InitialHandshakeProtocol;

namespace Client.Network.API
{
    public class InitialHandshakeResponse
    {
        public Vector3 PlayerPos { get; }
        public WorldDataResponse WorldData { get; }
        public List<MapObjectsInfoMessagePack> MapObjects { get; }
        public PlayerInventoryResponse Inventory { get; }
        public List<ChallengeCategoryResponse> Challenges { get; }
        public UnlockStateResponse UnlockState { get; }
        public List<string> PlayedSkitIds { get; }
        public Dictionary<Guid, ResearchNodeState> ResearchNodeStates { get; }
        // ログイン時の乗車復帰情報（未乗車なら RidingTarget は null）。
        // Login-time riding restore info (RidingTarget is null when not riding).
        public RidableIdentifierMessagePack RidingTarget { get; }
        public int RidingSeatIndex { get; }
        public ResponseMapDataMessagePack MapLayout { get; }
        // ログイン時のホットバー9枠。初期データ同梱のため追加の往復も未取得状態も無い
        // The login-time hotbar slots; bundled as initial data, so there is no extra round trip and no unfetched state
        public Guid[] HotbarAssignments { get; }
        // 残り設置数の全財布。handshake同梱のため追加往復は無い
        // All wallets' remaining-placement counts; bundled as initial data, so no extra round trip is needed
        public RemainingPlacementCountChangedEventPacket.RemainingPlacementCountMessagePack[] RemainingPlacementCounts { get; }

        public InitialHandshakeResponse(
            ResponseInitialHandshakeMessagePack initialHandshake,
            (
                List<MapObjectsInfoMessagePack> mapObjects,
                WorldDataResponse worldData,
                PlayerInventoryResponse inventory,
                List<ChallengeCategoryResponse> challenges,
                UnlockStateResponse unlockState,
                List<string> playedSkitIds,
                Dictionary<Guid, ResearchNodeState> researchNodeStates,
                ResponseMapDataMessagePack mapLayout) responses)
        {
            PlayerPos = initialHandshake.PlayerPos;
            WorldData = responses.worldData;
            MapObjects = responses.mapObjects;
            Inventory = responses.inventory;
            Challenges = responses.challenges;
            UnlockState = responses.unlockState;
            PlayedSkitIds = responses.playedSkitIds;
            ResearchNodeStates = responses.researchNodeStates;
            RidingTarget = initialHandshake.RidingTarget;
            RidingSeatIndex = initialHandshake.RidingSeatIndex;
            MapLayout = responses.mapLayout;
            HotbarAssignments = initialHandshake.HotbarAssignments;
            RemainingPlacementCounts = initialHandshake.RemainingPlacementCounts;
        }
    }
}
