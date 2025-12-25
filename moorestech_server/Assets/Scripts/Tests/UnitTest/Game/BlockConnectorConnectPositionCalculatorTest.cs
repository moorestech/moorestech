using System.Collections.Generic;
using System.Linq;
using Game.Block.Component;
using Game.Block.Interface;
using Mooresmaster.Model.BlockConnectInfoModule;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game
{
    public class BlockConnectorConnectPositionCalculatorTest
    {
        
        [Test]
        public void Calculate_2x2_North_Test()
        {
            var direction = BlockDirection.North;
            
            var blockPositionInfo = new BlockPositionInfo(Vector3Int.zero, direction, new Vector3Int(2, 2, 2));
            
            var connectInfoItems = new List<BlockConnectInfoElement>
            {
                new(0, "", new Vector3Int(1, 0, 0), new []{new Vector3Int(1,0,0)}, null),
            };
            var connectionInfo = new BlockConnectInfo(connectInfoItems.ToArray());
            
            
            var result = BlockConnectorConnectPositionCalculator.CalculateConnectPosToConnector(connectionInfo, blockPositionInfo);
            
            Assert.AreEqual(1, result.Count);
            AssertConnectors(result, 0, new Vector3Int(1, 0, 0), new Vector3Int(2, 0, 0));
        }
        
        /// <summary>
        /// 2x2のブロックを、北向きに設置した際に南東のコネクターが東向きにあるとき、そのブロックを東向きに設置して正しく位置が計算されていることを確認するテスト
        /// A test to confirm that a 2x2 block is placed facing north and the southeast connector faces east by placing the block facing east and correctly calculating the position.
        /// </summary>
        [Test]
        public void Calculate_2x2_East_Test()
        {
            var direction = BlockDirection.East;
            
            var blockPositionInfo = new BlockPositionInfo(Vector3Int.zero, direction, new Vector3Int(2, 2, 2));
            
            var connectInfoItems = new List<BlockConnectInfoElement>
            {
                new(0, "", new Vector3Int(1, 0, 0), new []{new Vector3Int(1,0,0)}, null),
            };
            var connectionInfo = new BlockConnectInfo(connectInfoItems.ToArray());
            
            
            var result = BlockConnectorConnectPositionCalculator.CalculateConnectPosToConnector(connectionInfo, blockPositionInfo);
            
            AssertConnectors(result, 0, Vector3Int.zero, new Vector3Int(0, 0, -1));
        }
        
        void AssertConnectors(Dictionary<Vector3Int, (Vector3Int position, BlockConnectInfoElement element)> result, int index, Vector3Int connectorPosition, Vector3Int targetPosition)
        {
            Assert.AreEqual(connectorPosition, result.Values.ToList()[index].position);
            Assert.AreEqual(targetPosition, result.Keys.ToList()[index]);
        }
    }
}