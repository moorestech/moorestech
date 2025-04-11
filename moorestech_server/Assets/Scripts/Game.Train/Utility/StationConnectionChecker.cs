using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.World.Interface.DataStore;
using Core.Master;
using Game.Context;
using Game.Train.RailGraph;
using UnityEditor;

namespace Game.Train.Utility
{
    public static class StationConnectionChecker
    {
        /// <summary>
        /// 自分（駅または貨物駅）のブロックについて、進行方向に同じカテゴリのブロックが隣接して接続している場合そのブロックの座標を返す。
        /// </summary>
        /// <param name="positionInfo">自分の駅または貨物駅のpositionInfo</param>
        public static (Vector3Int, bool) IsStationConnectedToFront(BlockPositionInfo positionInfo)
        {
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            /*
            railComponentAの座標(0.00, 0.50, 2.50)
            railComponentBの座標(22.00, 0.50, 2.50)
            railComponentAの座標(2.50, 5.50, 22.00)
            railComponentBの座標(2.50, 5.50, 0.00)
            railComponentAの座標(22.00, 10.50, 2.50)
            railComponentBの座標(0.00, 10.50, 2.50)
            railComponentAの座標(2.50, 15.50, 0.00)
            railComponentBの座標(2.50, 15.50, 22.00)
            */
            IBlock block;
            const int loopmax = 256;
            Vector3Int checkingpos = new Vector3Int(0, 0, 0);//チェックしたい座標
            (Vector3Int, bool) baddata = (new Vector3Int(0, -99999999, 0), false);//yが-99999999の時は接続していないという意味。まぁfalseで判定すればいい
            //positionInfo.BlockDirectionの4方向しだいで処理がわかる
            switch (positionInfo.BlockDirection)
            {
                case BlockDirection.North://固定値を調べればいい
                    checkingpos = new Vector3Int(positionInfo.OriginalPos.x + positionInfo.BlockSize.x, positionInfo.OriginalPos.y, positionInfo.OriginalPos.z);
                    block = worldBlockDatastore.GetBlock(checkingpos);
                    if (block == null) return baddata;
                    if (block.BlockMasterElement.BlockType != "TrainStation" && block.BlockMasterElement.BlockType != "TrainCargoPlatform") return baddata;
                    if (block.BlockPositionInfo.BlockDirection != BlockDirection.North) return baddata;
                    return (checkingpos, true);
                    break;
                case BlockDirection.East://zをマイナス方向にみていく
                    for (int i = 0; i < loopmax; i++)
                    {
                        checkingpos = new Vector3Int(positionInfo.OriginalPos.x, positionInfo.OriginalPos.y, positionInfo.OriginalPos.z - i - 1);
                        block = worldBlockDatastore.GetBlock(checkingpos);
                        if (block == null) continue;
                        if (block.BlockMasterElement.BlockType != "TrainStation" && block.BlockMasterElement.BlockType != "TrainCargoPlatform") continue;
                        if (block.BlockPositionInfo.BlockDirection != BlockDirection.East) continue;
                        if (block.BlockPositionInfo.OriginalPos.x != i + 1) continue;
                        return (checkingpos, true);
                    }
                    break;
                case BlockDirection.South://xをマイナス方向にみていく
                    for (int i = 0; i < loopmax; i++)
                    {
                        checkingpos = new Vector3Int(positionInfo.OriginalPos.x - i - 1, positionInfo.OriginalPos.y, positionInfo.OriginalPos.z);
                        block = worldBlockDatastore.GetBlock(checkingpos);
                        if (block == null) continue;
                        if (block.BlockMasterElement.BlockType != "TrainStation" && block.BlockMasterElement.BlockType != "TrainCargoPlatform") continue;
                        if (block.BlockPositionInfo.BlockDirection != BlockDirection.South) continue;
                        if (block.BlockPositionInfo.OriginalPos.x != i + 1) continue;
                        return (checkingpos, true);
                    }
                    break;
                case BlockDirection.West://固定値を調べればいい
                    checkingpos = new Vector3Int(positionInfo.OriginalPos.x, positionInfo.OriginalPos.y, positionInfo.OriginalPos.z + positionInfo.BlockSize.x);
                    block = worldBlockDatastore.GetBlock(checkingpos);
                    if (block == null) return baddata;
                    if (block.BlockMasterElement.BlockType != "TrainStation" && block.BlockMasterElement.BlockType != "TrainCargoPlatform") return baddata;
                    if (block.BlockPositionInfo.BlockDirection != BlockDirection.West) return baddata;
                    return (checkingpos, true);
                    break;
                default:
                    return baddata;
                    break;
            }
            return baddata;
        }

        /// <summary>
        /// 自分（駅または貨物駅）のブロックについて、逆方向に同じカテゴリのブロックが隣接して接続している場合そのブロックの座標を返す。
        /// </summary>
        /// <param name="positionInfo">自分の駅または貨物駅のpositionInfo</param>
        
        ここにコードを
    }
}