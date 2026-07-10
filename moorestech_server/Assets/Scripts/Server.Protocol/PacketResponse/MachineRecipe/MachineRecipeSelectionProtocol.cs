using System;
using Game.Entity.Interface;
using Game.Block.Interface.State;
using Game.Context;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    public class MachineRecipeSelectionProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:machineRecipeSelection";
        private const float MaxOperationDistance = 100f;
        private readonly IEntitiesDatastore _entitiesDatastore;
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;

        public MachineRecipeSelectionProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
            _entitiesDatastore = serviceProvider.GetService<IEntitiesDatastore>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            var request = MessagePackSerializer.Deserialize<MachineRecipeSelectionRequest>(payload);

            // 接続プレイヤーと対象機械を先に確定する
            // Resolve the connected player and target machine before dispatching the operation
            if (!context.PlayerId.HasValue) return Fail(MachineRecipeSelectionFailureReason.NotHandshaken, null);
            if (request == null || request.Position == null) return Fail(MachineRecipeSelectionFailureReason.InvalidRequest, null);

            // 接続プレイヤーの現在位置から操作可能な範囲だけを許可する
            // Allow operations only within range of the connected player's current position
            var playerId = context.PlayerId.Value;
            var playerEntityId = new EntityInstanceId(playerId);
            if (!_entitiesDatastore.Exists(playerEntityId)) return Fail(MachineRecipeSelectionFailureReason.NotAuthorized, null);
            var blockPosition = request.Position.Vector3Int;
            var blockCenter = blockPosition + Vector3.one * 0.5f;
            if (MaxOperationDistance * MaxOperationDistance < (blockCenter - _entitiesDatastore.GetPosition(playerEntityId)).sqrMagnitude)
                return Fail(MachineRecipeSelectionFailureReason.TooFar, null);

            var block = ServerContext.WorldBlockDatastore.GetBlock(blockPosition);
            if (block == null) return Fail(MachineRecipeSelectionFailureReason.BlockNotFound, null);
            if (!block.ComponentManager.TryGetComponent<IMachineRecipeSelectable>(out var processor))
                return Fail(MachineRecipeSelectionFailureReason.NotMachine, null);

            // nullは未選択、GUIDを厳密解析
            // Treat null as unselected; strictly parse GUIDs
            Guid? recipeGuid = null;
            if (request.RecipeGuid != null)
            {
                if (!Guid.TryParse(request.RecipeGuid, out var parsedRecipeGuid))
                    return Fail(MachineRecipeSelectionFailureReason.InvalidRequest, processor.RecipeGuid);
                recipeGuid = parsedRecipeGuid;
            }

            var playerInventory = _playerInventoryDataStore.GetInventoryData(playerId).MainOpenableInventory;
            var result = processor.TrySetRecipe(recipeGuid, playerInventory);
            var reason = MapFailureReason(result);
            return new MachineRecipeSelectionResponse(reason, processor.RecipeGuid);

            #region Internal

            static MachineRecipeSelectionResponse Fail(MachineRecipeSelectionFailureReason reason, Guid? appliedRecipeGuid)
            {
                return new MachineRecipeSelectionResponse(reason, appliedRecipeGuid);
            }

            static MachineRecipeSelectionFailureReason MapFailureReason(MachineRecipeChangeResult result)
            {
                return result switch
                {
                    MachineRecipeChangeResult.Success => MachineRecipeSelectionFailureReason.None,
                    MachineRecipeChangeResult.RecipeNotFound => MachineRecipeSelectionFailureReason.RecipeNotFound,
                    MachineRecipeChangeResult.RecipeForDifferentBlock => MachineRecipeSelectionFailureReason.RecipeForDifferentBlock,
                    MachineRecipeChangeResult.RecipeLocked => MachineRecipeSelectionFailureReason.RecipeLocked,
                    MachineRecipeChangeResult.RefundCapacityInsufficient => MachineRecipeSelectionFailureReason.RefundCapacityInsufficient,
                    _ => MachineRecipeSelectionFailureReason.InvalidRequest,
                };
            }

            #endregion
        }

        [MessagePackObject]
        public class MachineRecipeSelectionRequest : ProtocolMessagePackBase
        {
            [Key(2)] public Vector3IntMessagePack Position { get; set; }
            [Key(3)] public string RecipeGuid { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public MachineRecipeSelectionRequest() { Tag = ProtocolTag; }

            private MachineRecipeSelectionRequest(Vector3Int position, Guid? recipeGuid)
            {
                Tag = ProtocolTag;
                Position = new Vector3IntMessagePack(position);
                RecipeGuid = recipeGuid?.ToString();
            }

            public static MachineRecipeSelectionRequest CreateSetRequest(Vector3Int position, Guid? recipeGuid)
            {
                return new MachineRecipeSelectionRequest(position, recipeGuid);
            }
        }

        [MessagePackObject]
        public class MachineRecipeSelectionResponse : ProtocolMessagePackBase
        {
            [Key(2)] public MachineRecipeSelectionFailureReason FailureReason { get; set; }
            [Key(3)] public string AppliedRecipeGuid { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public MachineRecipeSelectionResponse() { }

            public MachineRecipeSelectionResponse(MachineRecipeSelectionFailureReason failureReason, Guid? appliedRecipeGuid)
            {
                Tag = ProtocolTag;
                FailureReason = failureReason;
                AppliedRecipeGuid = appliedRecipeGuid?.ToString();
            }

            public Guid? GetAppliedRecipeGuid()
            {
                return AppliedRecipeGuid == null ? null : Guid.Parse(AppliedRecipeGuid);
            }
        }

        public enum MachineRecipeSelectionFailureReason
        {
            None,
            NotHandshaken,
            BlockNotFound,
            NotMachine,
            RecipeNotFound,
            RecipeForDifferentBlock,
            RecipeLocked,
            RefundCapacityInsufficient,
            InvalidRequest,
            NotAuthorized,
            TooFar,
        }
    }
}
