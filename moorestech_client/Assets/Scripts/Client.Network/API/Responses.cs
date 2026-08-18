using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface;
using Game.Context;
using Game.Train.Unit;
using Mooresmaster.Model.ChallengesModule;
using Server.Event.EventReceive;
using Server.Util.MessagePack;
using UnityEngine;
using static Server.Protocol.PacketResponse.PlayerInventoryResponseProtocol;

namespace Client.Network.API
{
    public class PlayerInventoryResponse
    {
        /// <summary>
        ///     応答messagepackからの変換はDTO側に置く（前例は同ファイルの <see cref="BlockInfo" />）。
        ///     装備は専用の取得プロトコルを持たずこの応答に同梱されるため、ここで必ず読み出す。
        ///     Conversion from the response messagepack lives in the DTO, following the BlockInfo precedent in this file.
        ///     Equipment has no dedicated fetch protocol and rides on this response, so it must be read here.
        /// </summary>
        public PlayerInventoryResponse(PlayerInventoryResponseProtocolMessagePack response)
        {
            var itemStackFactory = ServerContext.ItemStackFactory;
            MainInventory = response.Main.Select(item => itemStackFactory.Create(item.Id, item.Count)).ToList();
            GrabItem = itemStackFactory.Create(response.Grab.Id, response.Grab.Count);
            Equipment = response.Equipment.Select(item => itemStackFactory.Create(item.Id, item.Count)).ToList();
            SelectedEquipmentIndex = response.SelectedEquipmentIndex;
        }

        public List<IItemStack> MainInventory { get; }
        public IItemStack GrabItem { get; }

        // 装備スロットの中身と、選択中スロット（-1は素手）
        // Equipment slot contents and the selected slot (-1 means bare hands)
        public List<IItemStack> Equipment { get; }
        public int SelectedEquipmentIndex { get; }
    }
    
    public class WorldDataResponse
    {
        public readonly List<BlockInfo> Blocks;
        public readonly List<EntityResponse> Entities;
        
        public WorldDataResponse(List<BlockInfo> blocks, List<EntityResponse> entities)
        {
            Blocks = blocks;
            Entities = entities;
        }
    }
    
    public class BlockInfo
    {
        public readonly BlockDirection BlockDirection;
        public readonly BlockId BlockId;
        public readonly Vector3Int BlockPos;
        public readonly BlockInstanceId BlockInstanceId;
        
        public BlockInfo(BlockDataMessagePack blockDataMessagePack)
        {
            BlockPos = blockDataMessagePack.BlockPos;
            BlockId = blockDataMessagePack.BlockId;
            BlockDirection = blockDataMessagePack.BlockDirection;
            BlockInstanceId = blockDataMessagePack.BlockInstanceId;
        }
    }
    
    public class EntityResponse
    {
        public readonly long InstanceId;
        public readonly Vector3 Position;
        public readonly string Type;
        
        public readonly byte[] EntityData;
        
        public EntityResponse(EntityMessagePack entityMessagePack)
        {
            InstanceId = entityMessagePack.InstanceId;
            Type = entityMessagePack.Type;
            Position = entityMessagePack.Position;
            EntityData = entityMessagePack.EntityData;
        }
    }
    
    public class ChallengeCategoryResponse
    {
        public readonly ChallengeCategoryMasterElement Category;
        public readonly bool IsUnlocked;
        
        public readonly List<ChallengeMasterElement> CurrentChallenges;
        public readonly List<ChallengeMasterElement> CompletedChallenges;

        
        public ChallengeCategoryResponse(ChallengeCategoryMasterElement category, bool isUnlocked, List<ChallengeMasterElement> currentChallenges, List<ChallengeMasterElement> completedChallenges)
        {
            Category = category;
            IsUnlocked = isUnlocked;
            CurrentChallenges = currentChallenges;
            CompletedChallenges = completedChallenges;
        }
    }
    
    public class UnlockStateResponse
    {
        // 名前付きの対応するオブジェクト初期化子で構築する。位置引数コンストラクタは持たない（C14裁定）
        // Built via a corresponding named object initializer; carries no positional constructor (C14 ruling)
        public List<Guid> LockedCraftRecipeGuids { get; set; }
        public List<Guid> UnlockedCraftRecipeGuids { get; set; }

        public List<ItemId> LockedItemIds { get; set; }
        public List<ItemId> UnlockedItemIds { get; set; }

        public List<Guid> LockedChallengeCategoryGuids { get; set; }
        public List<Guid> UnlockedChallengeCategoryGuids { get; set; }

        public List<Guid> LockedMachineRecipeGuids { get; set; }
        public List<Guid> UnlockedMachineRecipeGuids { get; set; }

        public List<Guid> LockedBlockGuids { get; set; }
        public List<Guid> UnlockedBlockGuids { get; set; }

        public List<Guid> LockedTrainCarGuids { get; set; }
        public List<Guid> UnlockedTrainCarGuids { get; set; }

        public List<Guid> LockedConnectToolGuids { get; set; }
        public List<Guid> UnlockedConnectToolGuids { get; set; }

        public bool IsBlueprintUnlocked { get; set; }
    }

    // 列車スナップショット取得時のレスポンス
    // Response wrapper for the initial train unit snapshot payload
    public class TrainUnitSnapshotResponse
    {
        public TrainUnitSnapshotResponse(List<TrainUnitSnapshotBundle> snapshots, uint serverTick, uint unitsHash, uint tickSequenceId)
        {
            Snapshots = snapshots ?? new List<TrainUnitSnapshotBundle>();
            ServerTick = serverTick;
            UnitsHash = unitsHash;
            TickSequenceId = tickSequenceId;
        }

        public List<TrainUnitSnapshotBundle> Snapshots { get; }
        public uint ServerTick { get; }
        public uint UnitsHash { get; }
        public uint TickSequenceId { get; }
    }
}
