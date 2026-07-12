using System;
using Core.Master;
using Game.Block.Interface.Component;
using Game.Context;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    /// 機械のレシピ選択を設定・解除するプロトコル。Operation により SetRecipe / Clear を切り替える。
    /// 選択状態の取得は既存のブロック状態同期（MachineBlockStateDetail）で行うため Get は持たない。
    /// Protocol to set/clear the machine recipe selection; reads go through the existing block state sync.
    /// </summary>
    public class MachineRecipeSelectionProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:machineRecipeSelection";

        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;

        public MachineRecipeSelectionProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            var request = MessagePackSerializer.Deserialize<MachineRecipeSelectionRequest>(payload);
            if (request.Position == null) return Fail(MachineRecipeSelectionFailureReason.InvalidRequest);

            var block = ServerContext.WorldBlockDatastore.GetBlock(request.Position.Vector3Int);
            if (block == null) return Fail(MachineRecipeSelectionFailureReason.BlockNotFound);
            if (!block.ComponentManager.TryGetComponent<IMachineRecipeSelectorComponent>(out var selector))
            {
                return Fail(MachineRecipeSelectionFailureReason.NotMachine);
            }

            // 加工中変更の返却先としてリクエスト元プレイヤーのメインインベントリを渡す
            // Pass the requesting player's main inventory as the refund overflow target
            var playerInventory = _playerInventoryDataStore.GetInventoryData(request.PlayerId).MainOpenableInventory;

            switch (request.Operation)
            {
                case MachineRecipeSelectionOperation.SetRecipe:
                    if (!Guid.TryParse(request.MachineRecipeGuidStr, out var recipeGuid)) return Fail(MachineRecipeSelectionFailureReason.InvalidRecipe);
                    var recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(recipeGuid);
                    if (recipe == null) return Fail(MachineRecipeSelectionFailureReason.InvalidRecipe);

                    var setResult = selector.SetSelectedRecipe(recipe, playerInventory);
                    if (setResult != MachineRecipeSelectionResult.Success) return Fail(ToFailureReason(setResult));
                    break;
                case MachineRecipeSelectionOperation.Clear:
                    var clearResult = selector.ClearSelectedRecipe(playerInventory);
                    if (clearResult != MachineRecipeSelectionResult.Success) return Fail(ToFailureReason(clearResult));
                    break;
                default:
                    return Fail(MachineRecipeSelectionFailureReason.UnknownOperation);
            }

            return new MachineRecipeSelectionResponse(true, MachineRecipeSelectionFailureReason.None, selector.SelectedRecipeGuid.ToString());

            #region Internal

            static MachineRecipeSelectionResponse Fail(MachineRecipeSelectionFailureReason reason)
            {
                return new MachineRecipeSelectionResponse(false, reason, Guid.Empty.ToString());
            }

            static MachineRecipeSelectionFailureReason ToFailureReason(MachineRecipeSelectionResult result)
            {
                return result switch
                {
                    MachineRecipeSelectionResult.RecipeBlockMismatch => MachineRecipeSelectionFailureReason.RecipeBlockMismatch,
                    MachineRecipeSelectionResult.RecipeLocked => MachineRecipeSelectionFailureReason.RecipeLocked,
                    MachineRecipeSelectionResult.RefundFailed => MachineRecipeSelectionFailureReason.RefundFailed,
                    _ => MachineRecipeSelectionFailureReason.UnknownOperation,
                };
            }

            #endregion
        }

        #region MessagePack

        [MessagePackObject]
        public class MachineRecipeSelectionRequest : ProtocolMessagePackBase
        {
            [Key(2)] public Vector3IntMessagePack Position { get; set; }
            [Key(3)] public MachineRecipeSelectionOperation Operation { get; set; }
            [Key(4)] public string MachineRecipeGuidStr { get; set; }
            [Key(5)] public int PlayerId { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public MachineRecipeSelectionRequest() { Tag = ProtocolTag; }

            // Operation ごとに必要なフィールドだけを設定する private コンストラクタ
            // Private constructor; static factories below set only the fields each Operation needs
            private MachineRecipeSelectionRequest(Vector3Int position, MachineRecipeSelectionOperation operation, string machineRecipeGuidStr, int playerId)
            {
                Tag = ProtocolTag;
                Position = new Vector3IntMessagePack(position);
                Operation = operation;
                MachineRecipeGuidStr = machineRecipeGuidStr;
                PlayerId = playerId;
            }

            public static MachineRecipeSelectionRequest CreateSetRecipeRequest(Vector3Int position, Guid machineRecipeGuid, int playerId)
            {
                return new MachineRecipeSelectionRequest(position, MachineRecipeSelectionOperation.SetRecipe, machineRecipeGuid.ToString(), playerId);
            }

            public static MachineRecipeSelectionRequest CreateClearRequest(Vector3Int position, int playerId)
            {
                return new MachineRecipeSelectionRequest(position, MachineRecipeSelectionOperation.Clear, Guid.Empty.ToString(), playerId);
            }
        }

        [MessagePackObject]
        public class MachineRecipeSelectionResponse : ProtocolMessagePackBase
        {
            [Key(2)] public bool Success { get; set; }
            [Key(3)] public MachineRecipeSelectionFailureReason FailureReason { get; set; }
            [Key(4)] public string SelectedRecipeGuid { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public MachineRecipeSelectionResponse() { }

            public MachineRecipeSelectionResponse(bool success, MachineRecipeSelectionFailureReason failureReason, string selectedRecipeGuid)
            {
                Tag = ProtocolTag;
                Success = success;
                FailureReason = failureReason;
                SelectedRecipeGuid = selectedRecipeGuid;
            }
        }

        public enum MachineRecipeSelectionOperation
        {
            SetRecipe = 0,
            Clear = 1,
        }

        public enum MachineRecipeSelectionFailureReason
        {
            None = 0,
            BlockNotFound = 1,
            NotMachine = 2,
            InvalidRecipe = 3,
            RecipeBlockMismatch = 4,
            RecipeLocked = 5,
            RefundFailed = 6,
            UnknownOperation = 7,
            InvalidRequest = 8,
        }

        #endregion
    }
}
