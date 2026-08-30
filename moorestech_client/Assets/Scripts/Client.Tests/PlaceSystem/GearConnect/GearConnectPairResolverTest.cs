using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.GearConnect;
using Core.Master;
using Game.Block.Interface;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.PlaceSystem.GearConnect
{
    /// <summary>
    ///     設置予定の歯車がどの隣接コネクタと噛み合うかを、サーバーと同じ判定で解けているかを検証する
    ///     Verifies that the meshing partners of a gear about to be placed resolve with the same rule the server uses
    /// </summary>
    public class GearConnectPairResolverTest
    {
        [Test]
        public void 発電機の隣のシャフトは1組の接続を返す()
        {
            CreateServer();
            // GearNetworkTest で実際に回る配置: 発電機(0,0,0)・シャフト(0,0,1)
            // The layout GearNetworkTest proves rotates: generator at (0,0,0), shaft at (0,0,1)
            var shaft = CreatePositionInfo(ForUnitTestModBlockId.Shaft, new Vector3Int(0, 0, 1));
            var generator = (ForUnitTestModBlockId.InfinityTorqueSimpleGearGenerator, CreatePositionInfo(ForUnitTestModBlockId.InfinityTorqueSimpleGearGenerator, Vector3Int.zero));

            var pairs = GearConnectPairResolver.Resolve(ForUnitTestModBlockId.Shaft, shaft, new List<(BlockId, BlockPositionInfo)> { generator });

            Assert.AreEqual(1, pairs.Count);
            Assert.AreEqual(new Vector3Int(0, 0, 1), pairs[0].SelfConnectorCell);
            Assert.AreEqual(new Vector3Int(0, 0, 0), pairs[0].TargetConnectorCell);
        }

        [Test]
        public void 離れたブロックとは接続しない()
        {
            CreateServer();
            var shaft = CreatePositionInfo(ForUnitTestModBlockId.Shaft, new Vector3Int(5, 0, 5));
            var generator = (ForUnitTestModBlockId.InfinityTorqueSimpleGearGenerator, CreatePositionInfo(ForUnitTestModBlockId.InfinityTorqueSimpleGearGenerator, Vector3Int.zero));

            var pairs = GearConnectPairResolver.Resolve(ForUnitTestModBlockId.Shaft, shaft, new List<(BlockId, BlockPositionInfo)> { generator });

            Assert.AreEqual(0, pairs.Count);
        }

        [Test]
        public void 歯車を持たないブロックは空を返す()
        {
            CreateServer();
            var chest = CreatePositionInfo(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, 1));

            var pairs = GearConnectPairResolver.Resolve(ForUnitTestModBlockId.ChestId, chest, new List<(BlockId, BlockPositionInfo)>());

            Assert.AreEqual(0, pairs.Count);
        }

        private static BlockPositionInfo CreatePositionInfo(BlockId blockId, Vector3Int position)
        {
            return new BlockPositionInfo(position, BlockDirection.North, MasterHolder.BlockMaster.GetBlockMaster(blockId).BlockSize);
        }

        private static void CreateServer()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }
    }
}
