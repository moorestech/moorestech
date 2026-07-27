using System;
using System.Collections.Generic;
using Game.PlayerRiding.Interface;
using Game.Research;
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
        }
    }
}
