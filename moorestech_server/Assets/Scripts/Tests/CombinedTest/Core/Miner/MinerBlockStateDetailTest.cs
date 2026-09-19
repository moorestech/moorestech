using System;
using System.Collections.Generic;
using System.Reflection;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Miner;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.Context;
using Game.EnergySystem;
using MessagePack;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module;
using Tests.Module.TestMod;
using Tests.Util;
using UniRx;
using UnityEngine;
using static Tests.CombinedTest.Core.Miner.MinerStateDetailTestUtil;

namespace Tests.CombinedTest.Core.Miner
{
    /// <summary>
    ///     採掘機のUI電力配信を検証
    ///     裁定2026-08-17・ADR0010準拠
    ///     Verifies the miner's UI power publishing
    ///     Per ruling 2026-08-17 / ADR 0010
    /// </summary>
    public class MinerBlockStateDetailTest
    {
        [Test]
        public void 採掘と待機の遷移tickでも配信する分子と分母は同じ状態基準になる()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var (miner, processor) = PlacePoweredMiner();
            var publishedDetails = new List<CommonMachineBlockStateDetail>();
            using var subscription = miner.BlockStateChange.Subscribe(state => publishedDetails.Add(ReadCommonDetail(state)));

            // 採掘⇄待機の両遷移tickを踏む
            // Exercise both mining/idle transition ticks
            GameUpdater.RunFrames(5);
            FillOutputSlots(processor);
            GameUpdater.RunFrames(5);
            ClearOutputSlots(processor);
            GameUpdater.RunFrames(5);

            // 発電量は十分なので、分子と分母が同じ基準なら全配信で充足率は1になる。分母0固着（=分子分母とも0）を見逃さないよう要求電力が正であることも確認する
            // Generation is ample, so every publish reads a satisfaction of 1 when both sides share one basis; also assert the request is positive so a stuck zero denominator cannot slip through
            Assert.Greater(publishedDetails.Count, 0, "配信が1件も無いと検査が空振りになる");
            foreach (var detail in publishedDetails)
            {
                Assert.Greater(detail.RequestPower, 0f, "要求電力が0固着している（配信ラッチ漏れ）");
                Assert.AreEqual(1f, detail.PowerRate, 0.001f, "遷移tickに分子と分母の状態基準がずれている");
            }
        }

        [Test]
        public void 歯車採掘機も採掘と待機の遷移tickでも配信する分子と分母は同じ状態基準になる()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var (miner, processor) = PlacePoweredGearMiner();
            var publishedDetails = new List<CommonMachineBlockStateDetail>();
            using var subscription = miner.BlockStateChange.Subscribe(state => publishedDetails.Add(ReadCommonDetail(state)));

            // 電気版と同じく両向きの遷移tickを踏ませる（登録順 gearMiner→processor の経路も対象に入れる）
            // Walk the same both-direction transitions as the electric version, exercising the gearMiner-then-processor registration order
            GameUpdater.RunFrames(5);
            FillOutputSlots(processor);
            GameUpdater.RunFrames(5);
            ClearOutputSlots(processor);
            GameUpdater.RunFrames(5);

            Assert.Greater(publishedDetails.Count, 0, "配信が1件も無いと検査が空振りになる");
            foreach (var detail in publishedDetails)
            {
                Assert.Greater(detail.RequestPower, 0f, "要求電力が0固着している（配信ラッチ漏れ）");
                Assert.AreEqual(1f, detail.PowerRate, 0.001f, "遷移tickに分子と分母の状態基準がずれている");
            }
        }

        [Test]
        public void 設置直後初回tick前でも要求電力はアイドル基準でラッチされている()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var (_, pos) = MinerMiningTest.GetItemMapVein();
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricMinerId, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var miner);

            var param = (ElectricMinerBlockParam)miner.BlockMasterElement.BlockParam;
            var detail = ReadCommonDetail(miner.GetBlockState());

            // ctorの初期ラッチのみ検証
            // Checks only the ctor's initial latch
            Assert.AreEqual(param.RequiredPower * param.IdlePowerRate, detail.RequestPower, 1e-4f, "設置直後の要求電力はアイドル基準でラッチされているはず");
            Assert.AreEqual(0f, detail.PowerRate, 1e-4f, "設置直後は供給が無いので充足率は0のはず");
        }

        [Test]
        public void 待機へ落ちたtickは前状態miningと現状態idleを発火つきで配信する()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var (miner, processor) = PlacePoweredMiner();
            GameUpdater.RunFrames(3);

            var published = new List<CommonMachineBlockStateDetail>();
            using var subscription = miner.BlockStateChange.Subscribe(state => published.Add(ReadCommonDetail(state)));
            FillOutputSlots(processor);
            GameUpdater.RunFrames(3);

            var dropCount = published.FindAll(detail => detail.PreviousStateType == VanillaMinerState.Mining.ToStr() && detail.CurrentStateType == VanillaMinerState.Idle.ToStr()).Count;
            Assert.AreEqual(1, dropCount, "採掘→待機の遷移は1回だけ配信されるはず");
        }

        [Test]
        public void 電柱を撤去した待機中の採掘機は供給電力0を発火つきで配信する()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var (miner, processor) = PlacePoweredMiner();
            FillOutputSlots(processor);
            GameUpdater.RunFrames(3);
            Assert.Greater(ReadCommonDetail(miner.GetBlockState()).CurrentPower, 0f, "待機中も待機分の電力は供給されているはず");

            // 給電断後は供給値が0に落ちる
            // Power loss drops the published value to zero
            var fired = 0;
            using var subscription = miner.BlockStateChange.Subscribe(_ => fired++);
            ServerContext.WorldBlockDatastore.RemoveBlock(miner.BlockPositionInfo.OriginalPos + PoleOffset, BlockRemoveReason.ManualRemove);
            GameUpdater.RunFrames(2);

            Assert.AreEqual(0f, ReadCommonDetail(miner.GetBlockState()).CurrentPower, 0.0001f, "無給電のtickでは供給電力は0");
            Assert.Greater(fired, 0, "待機中でも配信値が動いたなら発火するはず");
        }

        [Test]
        public void 給電中の待機採掘機は配信値が動かない限り毎tick発火しない()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var (miner, processor) = PlacePoweredMiner();
            FillOutputSlots(processor);
            GameUpdater.RunFrames(3);

            // 待機へ落ちた後は供給値も状態も動かないので、状態配信は発火しない
            // After settling into idle neither supply nor state moves, so no state publish fires
            var fired = 0;
            using var subscription = miner.BlockStateChange.Subscribe(_ => fired++);
            GameUpdater.RunFrames(10);
            Assert.AreEqual(0, fired, "給電中の待機採掘機が毎tick発火している");
        }
    }
}
