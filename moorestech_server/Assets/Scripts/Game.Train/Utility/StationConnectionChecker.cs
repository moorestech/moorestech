using UnityEngine;
using Game.Block.Interface;
using Game.Context;

namespace Game.Train.Utility
{
    public static class StationConnectionChecker
    {
        /// <summary>
        /// 自分（駅または貨物駅）のブロックについて、進行方向に同じカテゴリのブロックが隣接して接続している場合、そのブロックの座標を返す。
        /// 接続していなければ(座標, false)を返す。  
        /// ※ここではBlockPositionInfo.BlockDirectionが水平（North, East, South, West）の場合のみを対象とする。
        /// </summary>
        /// <param name="positionInfo">自分の駅または貨物駅のBlockPositionInfo</param>
        /// <returns>(接続先ブロックの座標, true) または (ダミー座標, false)</returns>
        public static (Vector3Int, bool) IsStationConnectedToFront(BlockPositionInfo positionInfo)
        {
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            IBlock block;
            const int loopmax = 256;
            Vector3Int checkingPos = Vector3Int.zero;
            //baddata: 接続なしの場合のダミー値。Y座標が極端な負の値であれば「接続なし」と判定する。
            (Vector3Int, bool) badData = (new Vector3Int(0, -99999999, 0), false);
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
            switch (positionInfo.BlockDirection)
            {
                case BlockDirection.North:
                    // 固定オフセット：自分の原点XにブロックのサイズX分を足す
                    checkingPos = new Vector3Int(
                        positionInfo.OriginalPos.x + positionInfo.BlockSize.x,
                        positionInfo.OriginalPos.y,
                        positionInfo.OriginalPos.z);
                    block = worldBlockDatastore.GetBlock(checkingPos);
                    if (!IsValidStationBlock(block, BlockDirection.North))
                        return badData;
                    return (checkingPos, true);

                case BlockDirection.East:
                    // 進行方向＝負Z方向にループ
                    for (int i = 0; i < loopmax; i++)
                    {
                        checkingPos = new Vector3Int(
                            positionInfo.OriginalPos.x,
                            positionInfo.OriginalPos.y,
                            positionInfo.OriginalPos.z - i - 1);
                        block = worldBlockDatastore.GetBlock(checkingPos);
                        if (block == null) continue;
                        if (!IsValidStationBlock(block, BlockDirection.East))
                            continue;
                        // 相手の長さがわからないので
                        if (block.BlockPositionInfo.OriginalPos.x != i + 1)
                            continue;
                        return (checkingPos, true);
                    }
                    break;

                case BlockDirection.South:
                    // 進行方向＝負X方向にループ
                    for (int i = 0; i < loopmax; i++)
                    {
                        checkingPos = new Vector3Int(
                            positionInfo.OriginalPos.x - i - 1,
                            positionInfo.OriginalPos.y,
                            positionInfo.OriginalPos.z);
                        block = worldBlockDatastore.GetBlock(checkingPos);
                        if (block == null) continue;
                        if (!IsValidStationBlock(block, BlockDirection.South))
                            continue;
                        if (block.BlockPositionInfo.OriginalPos.x != i + 1)
                            continue;
                        return (checkingPos, true);
                    }
                    break;

                case BlockDirection.West:
                    // 相手の長さがわからないので
                    checkingPos = new Vector3Int(
                        positionInfo.OriginalPos.x,
                        positionInfo.OriginalPos.y,
                        positionInfo.OriginalPos.z + positionInfo.BlockSize.x);
                    block = worldBlockDatastore.GetBlock(checkingPos);
                    if (!IsValidStationBlock(block, BlockDirection.West))
                        return badData;
                    return (checkingPos, true);

                default:
                    return badData;
            }
            return badData;
        }


        /// <summary>
        /// 自分（駅または貨物駅）のブロックについて、逆方向に同じカテゴリのブロックが隣接して接続している場合、そのブロックの座標を返す。
        /// </summary>
        /// <param name="positionInfo">自分の駅または貨物駅のBlockPositionInfo</param>
        /// <returns>(接続先ブロックの座標, true) または (ダミー座標, false)</returns>
        public static (Vector3Int, bool) IsStationConnectedToBack(BlockPositionInfo positionInfo)
        {
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            IBlock block;
            const int loopmax = 256;
            Vector3Int checkingPos = Vector3Int.zero;
            (Vector3Int, bool) badData = (new Vector3Int(0, -99999999, 0), false);
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
            switch (positionInfo.BlockDirection)
            {
                case BlockDirection.South:
                    // 固定オフセット：自分の原点XにブロックのサイズX分を足す
                    checkingPos = new Vector3Int(
                        positionInfo.OriginalPos.x + positionInfo.BlockSize.x,
                        positionInfo.OriginalPos.y,
                        positionInfo.OriginalPos.z);
                    block = worldBlockDatastore.GetBlock(checkingPos);
                    if (!IsValidStationBlock(block, BlockDirection.South))
                        return badData;
                    return (checkingPos, true);

                case BlockDirection.West:
                    // 進行方向＝負Z方向にループ
                    for (int i = 0; i < loopmax; i++)
                    {
                        checkingPos = new Vector3Int(
                            positionInfo.OriginalPos.x,
                            positionInfo.OriginalPos.y,
                            positionInfo.OriginalPos.z - i - 1);
                        block = worldBlockDatastore.GetBlock(checkingPos);
                        if (block == null) continue;
                        if (!IsValidStationBlock(block, BlockDirection.West))
                            continue;
                        // 相手の長さがわからないので
                        if (block.BlockPositionInfo.OriginalPos.x != i + 1)
                            continue;
                        return (checkingPos, true);
                    }
                    break;

                case BlockDirection.North:
                    // 進行方向＝負X方向にループ
                    for (int i = 0; i < loopmax; i++)
                    {
                        checkingPos = new Vector3Int(
                            positionInfo.OriginalPos.x - i - 1,
                            positionInfo.OriginalPos.y,
                            positionInfo.OriginalPos.z);
                        block = worldBlockDatastore.GetBlock(checkingPos);
                        if (block == null) continue;
                        if (!IsValidStationBlock(block, BlockDirection.North))
                            continue;
                        if (block.BlockPositionInfo.OriginalPos.x != i + 1)
                            continue;
                        return (checkingPos, true);
                    }
                    break;

                case BlockDirection.East:
                    // 固定オフセット：自分の原点ZにブロックのサイズX分を足す（ここではサイズXを採用している）
                    checkingPos = new Vector3Int(
                        positionInfo.OriginalPos.x,
                        positionInfo.OriginalPos.y,
                        positionInfo.OriginalPos.z + positionInfo.BlockSize.x);
                    block = worldBlockDatastore.GetBlock(checkingPos);
                    if (!IsValidStationBlock(block, BlockDirection.East))
                        return badData;
                    return (checkingPos, true);

                default:
                    return badData;
            }
            return badData;
        }


        /// <summary>
        /// 指定したブロックが駅または貨物駅であり、かつ期待する方向(expectedDirection)と一致するかを判定する。
        /// </summary>
        /// <param name="block">チェックするブロック</param>
        /// <param name="expectedDirection">期待するBlockDirection。Front側の場合は自分と同一、Back側の場合は逆方向</param>
        /// <returns>有効な場合は true、そうでなければ false</returns>
        private static bool IsValidStationBlock(IBlock block, BlockDirection expectedDirection)
        {
            if (block == null) return false;
            string type = block.BlockMasterElement.BlockType;
            if (type != "TrainStation" && type != "TrainCargoPlatform")
                return false;
            if (block.BlockPositionInfo.BlockDirection != expectedDirection)
                return false;
            return true;
        }
    }
}
