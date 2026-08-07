using System.Collections.Generic;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Component.ConnectJudge;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Game.Block.Component
{
    public class InventoryContext<TTarget> : IConnectorContext<TTarget> where TTarget : IBlockInventory
    {
        public List<Vector3Int> InitializeAndGetOverridelSubsrcibePositions(IBlockConnectorComponent<TTarget> component, BlockPositionInfo positionInfo)
        {
            var block = ServerContext.WorldBlockDatastore.GetBlock(positionInfo.OriginalPos);
            
            // ベルトコンベアじゃない時は即終了　メモ：通常インベントリとベルトコンベアの接続処理についても検討を行う
            if (block.BlockMasterElement.BlockParam is not IBeltConveyorParam) return new List<Vector3Int>();
            
            var beltConveyorType = (block.BlockMasterElement.BlockParam as IBeltConveyorParam).SlopeType;
            
            var connectEdges = GetBeltConveyorEdes(block); 
            
            var subsribePositoons = ブロックのエッジから具体的なワールド座標を計算するメソッド(connectEdges);
            
            return subsribePositoons;
        }
        
        public Dictionary<TTarget, ConnectedInfo> GetOverride(Dictionary<TTarget, ConnectedInfo> currentTarget, IBlock targetBlock)
        {
            // 相手がベルトコンベアでなければそのまま即終了
            if (targetBlock.BlockMasterElement.BlockParam is not IBeltConveyorParam) return currentTarget;
            
            // 相手の辺の情報を取得
            var connectEdges = GetBeltConveyorEdes(targetBlock); 
            
            var selftConnectEdges = GetBeltConveyorEdes(selfBlock);
            
            // 自分の辺と突き合わせる　なんかいい感じの接続判定処理
            エッジ同士で判定するメソッド(connectEdges, selftConnectEdges);
            周囲のブロックの関係をみて決めるメソッド();
            
        }
        
        public bool CanConnect(ConnectJudgeContext context)
        {
            return true;
        }
        
        static List<Vector3Int> GetBeltConveyorEdes(IBlock block)
        {
            var positonInfo = block.BlockPositionInfo;
            
            
            var connectEdges = new List<Vector3Int>();
            
            var beltConveyorType = (block.BlockMasterElement.BlockParam as IBeltConveyorParam).SlopeType;
            switch (beltConveyorType)
            {
                case BeltConveyorBlockParam.SlopeTypeConst.Down:
                    connectEdges.Add(new Vector3Int(1,1,0));
                    break;
                
                case BeltConveyorBlockParam.SlopeTypeConst.Up:
                    break;
                
                case BeltConveyorBlockParam.SlopeTypeConst.Straight:
                    break;
                
            }
            
            // ローカルのエッジ座標をワールドに変換する処理
            var worldEdges = ほげほげ(connectEdges); 
            
            return worldEdges;
        }
    }
}