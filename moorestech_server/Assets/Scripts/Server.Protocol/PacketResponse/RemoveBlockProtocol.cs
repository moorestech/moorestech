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
using Game.Train.RailGraph;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Protocol.PacketResponse.Util.Construction;
using Server.Protocol.PacketResponse.Util.RailEdit;
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
        private readonly IRailGraphDatastore _railGraphDatastore;


        public RemoveBlockProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
            _railPositionManager = serviceProvider.GetService<TrainRailPositionManager>();
            _constructionWallet = serviceProvider.GetService<ConstructionWalletService>();
            _railGraphDatastore = serviceProvider.GetService<IRailGraphDatastore>();
        }
        
        public ProtocolMessagePackBase GetResponse(byte[] payload, int requesterPlayerId)
        {
            var data = MessagePackSerializer.Deserialize<RemoveBlockProtocolMessagePack>(payload);
            
            var block = ServerContext.WorldBlockDatastore.GetBlock(data.Pos);
            if (block == null) return Refuse(RemoveBlockFailureReason.Unknown);
            if (!RailBlockRemovalGuard.CanRemove(block, _railPositionManager)) return Refuse(RemoveBlockFailureReason.NodeInUseByTrain);

            // 財布に返却物を問い合わせ（確定は後段）
            // Ask the wallet what to refund (finalized further down)
            var removalPlan = _constructionWallet.PlanRemoval(block.BlockId, block.BlockInstanceId, requesterPlayerId);

            // レール返却を合算し全額入る時だけ撤去
            // Sum rail refunds and remove only if the whole refund fits
            var railRefundItems = RailRemovalRefundCalculator.CreateRefundItems(block, _railGraphDatastore);
            if (!TryInsertRefundItems(out var refundItems))
                return Refuse(RemoveBlockFailureReason.InventoryFull);
            
            // 削除処理
            // Deletion process
            ServerContext.WorldBlockDatastore.RemoveBlock(data.Pos, BlockRemoveReason.ManualRemove);

            // 撤去確定後に財布へ知らせる
            // Tell the wallet once the removal is final
            _constructionWallet.CommitRemoval(removalPlan);

            InsertItemsToPlayerInventory(refundItems);

            // 財布の変更通知は撤去1回につき1通へ集約する
            // Collapse the wallet notifications into one per removal
            _constructionWallet.FlushRemainingCountChanges();
            
            return RemoveBlockResponseMessagePack.CreateSuccess();
            
            #region Internal

            RemoveBlockResponseMessagePack Refuse(RemoveBlockFailureReason reason)
            {
                // 拒否した対象と理由を記録する
                // Record the refused target and reason
                Debug.Log($"[RemoveBlock] removal denied: {reason} position={data.Pos.Vector3Int}");
                return RemoveBlockResponseMessagePack.CreateFailure(reason);
            }

            bool TryInsertRefundItems(out List<IItemStack> items)
            {
                var playerMainInventory = _playerInventoryDataStore.GetInventoryData(requesterPlayerId).MainOpenableInventory;
                items = GetRefundItems();
                
                return playerMainInventory.InsertionCheck(items);
            }
            
            
            List<IItemStack> GetRefundItems()
            {
                var result = new List<IItemStack>();
                
                // 建設コストの返却は財布の指示に従うだけ
                // The construction-cost refund simply follows the wallet's instruction
                result.AddRange(removalPlan.ItemsToRefund);
                
                // 開けるインベントリのスロットを返す。ベルコンのようにスロットを持たないblockは何も返さない
                // Refund the slots of an openable inventory; blocks without slots, such as belts, refund nothing
                if (ServerContext.WorldBlockDatastore.TryGetBlock<IOpenableBlockInventoryComponent>(data.Pos, out var blockInventory))
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
                
                result.AddRange(railRefundItems);
                return result;
            }
            
            void InsertItemsToPlayerInventory(List<IItemStack> items)
            {
                var playerMainInventory = _playerInventoryDataStore.GetInventoryData(requesterPlayerId).MainOpenableInventory;
                playerMainInventory.InsertItem(items);
            }
            
            #endregion
        }
        
        
        [MessagePackObject]
        public class RemoveBlockProtocolMessagePack : ProtocolMessagePackBase
        {
            [Key(3)] public Vector3IntMessagePack Pos { get; set; }
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RemoveBlockProtocolMessagePack() { }
            public RemoveBlockProtocolMessagePack(Vector3Int pos)
            {
                Tag = ProtocolTag;
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
            InventoryFull,
        }
    }
}
