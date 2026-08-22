using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.Train.RailPositions;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Protocol.PacketResponse.Util.Construction;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    public class RemoveBlockProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:removeBlock";
        
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;
        private readonly TrainRailPositionManager _railPositionManager;
        private readonly ConstructionWalletService _constructionWallet;


        public RemoveBlockProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
            _railPositionManager = serviceProvider.GetService<TrainRailPositionManager>();
            _constructionWallet = serviceProvider.GetService<ConstructionWalletService>();
        }
        
        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            var data = MessagePackSerializer.Deserialize<RemoveBlockProtocolMessagePack>(payload);
            
            var block = ServerContext.WorldBlockDatastore.GetBlock(data.Pos);
            if (block == null) return RemoveBlockResponseMessagePack.CreateFailure(RemoveBlockFailureReason.Unknown);
            if (!CanManualRemoveBlock(block)) return RemoveBlockResponseMessagePack.CreateFailure(RemoveBlockFailureReason.NodeInUseByTrain);

            // 財布に返却物を問い合わせ（確定は後段）
            // Ask the wallet what to refund (finalized further down)
            var removalPlan = _constructionWallet.PlanRemoval(MasterHolder.BlockMaster.GetBlockMaster(block.BlockId), data.PlayerId);

            // 破壊した後のアイテムをインベントリに挿入できるかチェック
            // Check if items after destruction can be inserted into inventory
            if (!TryInsertRefundItems(out var refundItems)) return RemoveBlockResponseMessagePack.CreateFailure(RemoveBlockFailureReason.Unknown);
            
            // 削除処理
            // Deletion process
            ServerContext.WorldBlockDatastore.RemoveBlock(data.Pos, BlockRemoveReason.ManualRemove);

            // 撤去確定後に財布へ知らせる
            // Tell the wallet once the removal is final
            _constructionWallet.CommitRemoval(removalPlan);

            InsertItemsToPlayerInventory(refundItems);
            
            return RemoveBlockResponseMessagePack.CreateSuccess();
            
            #region Internal

            bool CanManualRemoveBlock(IBlock targetBlock)
            {
                var railComponents = targetBlock.ComponentManager.GetComponents<RailComponent>();
                if (railComponents.Count == 0) return true;

                // レール系ブロックは列車位置が保持するノードを壊せない
                // Rail blocks cannot remove nodes currently held by train positions.
                for (var i = 0; i < railComponents.Count; i++)
                {
                    if (!CanManualRemoveRailComponent(railComponents[i])) return false;
                }

                return true;
            }

            bool CanManualRemoveRailComponent(RailComponent railComponent)
            {
                // 橋脚削除はFront/Back両ノードの削除と同義として扱う
                // Removing a pier is equivalent to removing both front and back nodes.
                if (!_railPositionManager.CanRemoveNode(railComponent.FrontNode)) return false;
                if (!_railPositionManager.CanRemoveNode(railComponent.BackNode)) return false;

                return true;
            }
            
            bool TryInsertRefundItems(out List<IItemStack> items)
            {
                var playerMainInventory = _playerInventoryDataStore.GetInventoryData(data.PlayerId).MainOpenableInventory;
                items = GetRefundItems();
                
                return playerMainInventory.InsertionCheck(items);
            }
            
            
            List<IItemStack> GetRefundItems()
            {
                var result = new List<IItemStack>();
                
                // 建設コストの返却は財布の指示に従うだけ
                // The construction-cost refund simply follows the wallet's instruction
                result.AddRange(removalPlan.ItemsToRefund);
                
                // インベントリのアイテムを取得
                // Get items from block inventory
                if (ServerContext.WorldBlockDatastore.TryGetBlock<IBlockInventory>(data.Pos, out var blockInventory))
                {
                    for (var i = 0; i < blockInventory.GetSlotSize(); i++)
                    {
                        result.Add(blockInventory.GetItem(i));
                    }
                }
                
                // その他の返却すべきアイテム情報を取得する
                // Get refundable item information before block removal
                if (block.ComponentManager.TryGetComponent(out IGetRefundItemsInfo refundInfo))
                {
                    result.AddRange(refundInfo.GetRefundItems());
                }
                
                return result;
            }
            
            void InsertItemsToPlayerInventory(List<IItemStack> items)
            {
                var playerMainInventory = _playerInventoryDataStore.GetInventoryData(data.PlayerId).MainOpenableInventory;
                playerMainInventory.InsertItem(items);
            }
            
            #endregion
        }
        
        
        [MessagePackObject]
        public class RemoveBlockProtocolMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public int PlayerId { get; set; }
            [Key(3)] public Vector3IntMessagePack Pos { get; set; }
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RemoveBlockProtocolMessagePack() { }
            public RemoveBlockProtocolMessagePack(int playerId, Vector3Int pos)
            {
                Tag = ProtocolTag;
                PlayerId = playerId;
                Pos = new Vector3IntMessagePack(pos);
            }
        }

        [MessagePackObject]
        public class RemoveBlockResponseMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public bool Success { get; set; }
            [Key(3)] public RemoveBlockFailureReason FailureReason { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RemoveBlockResponseMessagePack() { Tag = ProtocolTag; }

            public RemoveBlockResponseMessagePack(bool success, RemoveBlockFailureReason failureReason)
            {
                Tag = ProtocolTag;
                Success = success;
                FailureReason = failureReason;
            }

            public static RemoveBlockResponseMessagePack CreateSuccess()
            {
                return new RemoveBlockResponseMessagePack(true, RemoveBlockFailureReason.None);
            }

            public static RemoveBlockResponseMessagePack CreateFailure(RemoveBlockFailureReason failureReason)
            {
                return new RemoveBlockResponseMessagePack(false, failureReason);
            }
        }

        public enum RemoveBlockFailureReason
        {
            None,
            NodeInUseByTrain,
            Unknown,
        }
    }
}
