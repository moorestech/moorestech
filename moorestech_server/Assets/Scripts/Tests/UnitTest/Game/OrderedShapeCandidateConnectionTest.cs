using System;
using System.Linq;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Component.ConnectJudge;
using Game.Block.Interface.Extension;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.UnitTest.Game
{
    /// <summary>
    ///     同一受入位置に形状の異なる複数の入力コネクター候補がある場合、
    ///     先頭候補が形状不適合でも後続候補で正しく接続されるかをテストする
    ///     Regression: when multiple input connector candidates exist at the same accepted
    ///     position with different shapes, connection must succeed via a later compatible candidate
    ///     even if the first-declared candidate is shape-incompatible
    /// </summary>
    public class OrderedShapeCandidateConnectionTest
    {
        [Test]
        public void FirstCandidateShapeIncompatibleButSecondCandidateConnectsTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            // ソース(shapeGuid=11)とターゲット(候補0:22→不適合、候補1:11→適合)を隣接設置
            // Place source (shapeGuid=11) next to target (candidate0=22 incompatible, candidate1=11 compatible)
            world.TryAddBlock(ForUnitTestModBlockId.TestShapeOrderSourceChest, new Vector3Int(0, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var sourceBlock);
            world.TryAddBlock(ForUnitTestModBlockId.TestShapeOrderTargetChest, new Vector3Int(1, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            var connectedTargets = sourceBlock.GetComponent<BlockConnectorComponent<IBlockInventory, DefaultConnectJudge>>().ConnectedTargets;

            // 形状不適合な候補で拒否されず、後続の適合候補で接続が成立していることを確認
            // Confirm connection succeeds via the later compatible candidate, not rejected by the incompatible one
            Assert.AreEqual(1, connectedTargets.Count);

            var connectedInfo = connectedTargets.First().Value;
            Guid? compatibleShapeGuid = Guid.Parse("11111111-1111-1111-1111-111111111111");
            Assert.AreEqual(compatibleShapeGuid, connectedInfo.TargetConnector.ShapeGuid);
        }
    }
}
