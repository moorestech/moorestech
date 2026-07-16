using System;
using Core.Update;
using Game.Block.Interface;
using Game.Context;
using Game.EnergySystem;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.Module.TestMod.ForUnitTestModBlockId;
using static Tests.Util.ElectricNetworkReflectionTestUtil;

namespace Tests.CombinedTest.Game.Energy
{
    /// <summary>
    ///     セグメントの橋渡し役の発電機を破壊したときに、残りが正しく分割されクラッシュしないことを検証する
    ///     Verify that destroying a bridging generator correctly splits the remainder without crashing
    /// </summary>
    public class RemoveGeneratorFromMultiSegmentTest
    {
        // 2本の電柱を繋ぐ発電機を削除すると、電柱が別々のセグメントへ分割される
        // Removing the generator that bridges two poles splits the poles into separate segments
        [Test]
        public void RemovingBridgingGeneratorSplitsSegmentWithoutCrash()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var networkDatastore = serviceProvider.GetService<IElectricWireNetworkLookup>();

            // 電柱 - 発電機 - 電柱 を一直線にワイヤー接続する
            // Wire pole - generator - pole in a straight line
            var generatorPos = new Vector3Int(2, 0, 0);
            worldBlockDatastore.TryAddBlock(ElectricPoleId, Pos(0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var pole1);
            worldBlockDatastore.TryAddBlock(InfinityGeneratorId, generatorPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            worldBlockDatastore.TryAddBlock(ElectricPoleId, Pos(4, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var pole2);
            ElectricWireTestUtil.Connect(Pos(0, 0), generatorPos);
            ElectricWireTestUtil.Connect(generatorPos, Pos(4, 0));
            GameUpdater.UpdateOneTick();

            // 発電機が橋渡しとなり全体が1セグメント
            // The generator bridges everything into a single segment
            Assert.AreEqual(1, GetSegmentCount(networkDatastore));

            // 発電機を破壊。橋渡しが消えるので両電柱は分断される
            // Destroy the generator; the bridge is gone so the two poles are separated
            worldBlockDatastore.RemoveBlock(generatorPos, BlockRemoveReason.ManualRemove);
            GameUpdater.UpdateOneTick();

            Assert.AreEqual(2, GetSegmentCount(networkDatastore));
            Assert.IsTrue(networkDatastore.TryGetEnergySegment(pole1.BlockInstanceId, out var segment1));
            Assert.IsTrue(networkDatastore.TryGetEnergySegment(pole2.BlockInstanceId, out var segment2));
            Assert.AreNotSame(segment1, segment2);

            // 破壊後にtickしても OutputEnergy が破壊済みコンポーネントを叩いて落ちないこと
            // Ticking after destruction must not crash by calling OutputEnergy on the destroyed component
            Assert.DoesNotThrow(() =>
            {
                for (var i = 0; i < 2; i++) GameUpdater.UpdateOneTick();
            });
        }

        private static Vector3Int Pos(int x, int z)
        {
            return new Vector3Int(x, 0, z);
        }
    }
}
