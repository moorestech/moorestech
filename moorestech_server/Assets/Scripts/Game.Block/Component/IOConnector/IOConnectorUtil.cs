using System.Collections.Generic;
using System.Linq;
using Game.Block.BlockInventory;
using Game.Block.Interface.BlockConfig;
using Game.World.Interface.DataStore;
using UnityEngine;

namespace Game.Block.Component.IOConnector
{
    public class IOConnectorUtil
    {
        private static readonly Dictionary<string, IOConnectionSetting> IOConnectionData = new()
        {
            {
                VanillaBlockType.Machine,
                new IOConnectionSetting(
                    new ConnectDirection[] { new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0) },
                    new ConnectDirection[] { new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0) },
                    new[] { VanillaBlockType.BeltConveyor })
            },
            {
                VanillaBlockType.Chest,
                new IOConnectionSetting(
                    new ConnectDirection[] { new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0) },
                    new ConnectDirection[] { new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0) },
                    new[] { VanillaBlockType.BeltConveyor })
            },
            {
                VanillaBlockType.Generator,
                new IOConnectionSetting(
                    new ConnectDirection[] { new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0) },
                    new ConnectDirection[] { },
                    new[] { VanillaBlockType.BeltConveyor })
            },
            {
                VanillaBlockType.Miner,
                new IOConnectionSetting(
                    new ConnectDirection[] { },
                    new ConnectDirection[] { new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0) },
                    new[] { VanillaBlockType.BeltConveyor })
            },
            {
                VanillaBlockType.BeltConveyor, new IOConnectionSetting(
                    // 南、西、東をからの接続を受け、アイテムをインプットする
                    new ConnectDirection[] { new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0) },
                    //北向きに出力する
                    new ConnectDirection[] { new(1, 0, 0) },
                    new[]
                    {
                        VanillaBlockType.Machine, VanillaBlockType.Chest, VanillaBlockType.Generator,
                        VanillaBlockType.Miner, VanillaBlockType.BeltConveyor
                    })
            }
        };
        
        
        /// <summary>
        ///     ブロックを接続元から接続先に接続できるなら接続する
        ///     その場所にブロックがあるか、
        ///     そのブロックのタイプはioConnectionDataDictionaryにあるか、
        ///     それぞれインプットとアウトプットの向きはあっているかを確認し、接続する
        /// </summary>
        private static void ConnectBlock(Vector3Int source, Vector3Int destination,IWorldBlockDatastore worldBlockDatastore,IBlockConfig blockConfig)
        {
            //接続元、接続先にBlockInventoryがなければ処理を終了
            if (!worldBlockDatastore.ExistsComponent<IBlockInventory>(source) ||
                !worldBlockDatastore.ExistsComponent<IBlockInventory>(destination)) return;


            //接続元のブロックデータを取得
            var sourceBlock = worldBlockDatastore.GetBlock(source);
            var sourceBlockType = blockConfig.GetBlockConfig(sourceBlock.BlockId).Type;
            //接続元のブロックタイプがDictionaryになければ処理を終了
            if (!IOConnectionData.ContainsKey(sourceBlockType)) return;

            var (_, sourceBlockOutputConnector) =
                GetConnectionPositions(
                    sourceBlockType,
                    worldBlockDatastore.GetBlockDirection(source));


            //接続先のブロックデータを取得
            var destinationBlock = worldBlockDatastore.GetBlock(destination);
            var destinationBlockType = blockConfig.GetBlockConfig(destinationBlock.BlockId).Type;
            //接続先のブロックタイプがDictionaryになければ処理を終了
            if (!IOConnectionData.ContainsKey(destinationBlockType)) return;

            var (destinationBlockInputConnector, _) = GetConnectionPositions(destinationBlockType, worldBlockDatastore.GetBlockDirection(destination));


            //接続元の接続可能リストに接続先がなかったら終了
            if (!IOConnectionData[sourceBlockType].ConnectableBlockType.Contains(destinationBlockType)) return;


            //接続元から接続先へのブロックの距離を取得
            var distance = destination - source;

            //接続元ブロックに対応するアウトプット座標があるかチェック
            if (!sourceBlockOutputConnector.Contains(new ConnectDirection(distance))) return;
            //接続先ブロックに対応するインプット座標があるかチェック
            if (!destinationBlockInputConnector.Contains(new ConnectDirection(distance * -1))) return;


            //接続元ブロックと接続先ブロックを接続
            worldBlockDatastore.GetBlock<IBlockInventory>(source).AddOutputConnector(
                worldBlockDatastore.GetBlock<IBlockInventory>(destination));
        }
        
        /// <summary>
        ///     接続先のブロックの接続可能な位置を取得する
        /// </summary>
        /// <param name="blockType"></param>
        /// <param name="blockDirection"></param>
        /// <returns></returns>
        private static (List<ConnectDirection>, List<ConnectDirection>) GetConnectionPositions(string blockType, BlockDirection blockDirection)
        {
            var rawInputConnector = IOConnectionData[blockType].InputConnector;
            var rawOutputConnector = IOConnectionData[blockType].OutputConnector;

            var blockPosConvertAction = blockDirection.GetCoordinateConvertAction();

            var inputPoss = rawInputConnector.Select(ConvertConnectDirection).ToList();
            var outputPoss = rawOutputConnector.Select(ConvertConnectDirection).ToList();

            return (inputPoss, outputPoss);

            #region Internal

            ConnectDirection ConvertConnectDirection(ConnectDirection connectDirection)
            {
                var convertedVector = blockPosConvertAction(connectDirection.ToVector3Int());
                return new ConnectDirection(convertedVector);
            }

            #endregion
        }
    }
}