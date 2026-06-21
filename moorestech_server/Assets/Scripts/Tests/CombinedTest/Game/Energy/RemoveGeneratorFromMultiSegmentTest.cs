using System;
using Core.Update;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.Module.TestMod.ForUnitTestModBlockId;

namespace Tests.CombinedTest.Game.Energy
{
    /// <summary>
    ///     複数セグメントに所属する発電機を破壊したときに、残存参照でtickがクラッシュしないことを検証する
    ///     Verify that destroying a generator belonging to multiple segments does not crash the tick via a dangling reference
    /// </summary>
    public class RemoveGeneratorFromMultiSegmentTest
    {
        [Test]
        public void RemovingGeneratorSharedByMultipleSegmentsDoesNotCrashOnNextTick()
        {
            var (_, serviceProvider) =
                new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            // 電柱を置かずに発電機のみ設置し、自動セグメント接続を発生させない
            // Place only the generator (no pole) so no automatic segment connection happens
            var generatorPos = new Vector3Int(0, 0, 0);
            worldBlockDatastore.TryAddBlock(InfinityGeneratorId, generatorPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var generatorBlock);
            var generator = generatorBlock.GetComponent<IElectricGenerator>();

            var segmentDatastore = serviceProvider.GetService<IWorldEnergySegmentDatastore<EnergySegment>>();

            // 範囲が重なる電柱構成を模し、1台の発電機を2つのセグメントへ登録する
            // Register one generator into two segments, emulating overlapping pole ranges
            var segmentA = segmentDatastore.CreateEnergySegment();
            segmentA.AddGenerator(generator);
            var segmentB = segmentDatastore.CreateEnergySegment();
            segmentB.AddGenerator(generator);

            // 発電機を破壊。全セグメントから外れないと破壊済み参照が残る
            // Destroy the generator; if it is not removed from every segment a dangling reference remains
            worldBlockDatastore.RemoveBlock(generatorPos, BlockRemoveReason.ManualRemove);

            // 破壊後にtickしても OutputEnergy が破壊済みコンポーネントを叩いて落ちないこと
            // Ticking after destruction must not crash by calling OutputEnergy on the destroyed component
            Assert.DoesNotThrow(() =>
            {
                for (var i = 0; i < 2; i++) GameUpdater.UpdateOneTick();
            });
        }
    }
}
