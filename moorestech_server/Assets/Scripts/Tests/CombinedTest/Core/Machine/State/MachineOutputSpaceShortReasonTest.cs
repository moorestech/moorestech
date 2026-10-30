using System;
using System.Linq;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Fluid;
using Game.UnlockState;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util;
using UniRx;
using UnityEngine;
using static Tests.CombinedTest.Core.Machine.MachineInventoryTestHelper;
using static Tests.CombinedTest.Core.Machine.MachineOutputSpaceShortTestHelper;

namespace Tests.CombinedTest.Core.Machine
{
    // 出力空き不足で止まった理由が、公開状態idleのまま状態詳細へ載り、解消で消えることを確かめる（Task31 r3-B F01）
    // Verifies the output-space-short reason rides the state detail while the published state stays idle, and clears on resolution (Task31 r3-B F01)
    public class MachineOutputSpaceShortReasonTest
    {
        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void 開始前に確定出力が入りきらない待機は状態イベントの時点で理由を載せ出口回収で消える()
        {
            var recipe = ItemRecipe();
            var (_, inventory, processor) = PlaceSelected(recipe, Vector3Int.one);
            var reasonAtLastNotice = false;
            processor.OnChangeBlockState.Subscribe(_ => reasonAtLastNotice = ReadOutputSpaceShort(processor));
            FillOutputSlot(recipe, inventory);
            InsertInputs(recipe, inventory);

            // 給電なしで1tick進めると、確定出力が入りきらず待機し、その tick の状態イベントが理由を運ぶ
            // One unpowered tick leaves the realized outputs unfit, and that tick's state event carries the reason
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(ProcessState.Idle, processor.CurrentState);
            Assert.AreEqual("idle", ReadPublishedState(processor));
            Assert.IsTrue(ReadOutputSpaceShort(processor));
            Assert.IsTrue(reasonAtLastNotice, "状態イベントの時点の詳細に理由が載る");

            // 出口を空けると加工が始まり理由が消える
            // Freeing the output starts the job and clears the reason
            EmptyOutputSlot(inventory);
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
            Assert.IsFalse(ReadOutputSpaceShort(processor));
        }

        [Test]
        public void 加工後の出力詰まりも同じ理由で公開状態はidleのまま出口回収で消える()
        {
            var recipe = ItemRecipe();
            var (_, inventory, processor) = PlaceSelected(recipe, Vector3Int.one);
            InsertInputs(recipe, inventory);
            processor.SupplyExternalPower(10000);
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
            Assert.IsFalse(ReadOutputSpaceShort(processor));

            // 加工中に出口を埋め、完了tickを跨ぐ
            // Fill the output mid-job and cross the completion tick
            FillOutputSlot(recipe, inventory);
            for (var tick = 0; tick < GameUpdater.SecondsToTicks(recipe.Time + 0.5); tick++)
            {
                processor.SupplyExternalPower(10000);
                GameUpdater.UpdateOneTick();
            }
            Assert.AreEqual(ProcessState.OutputBlocked, processor.CurrentState);
            Assert.AreEqual("idle", ReadPublishedState(processor));
            Assert.IsTrue(ReadOutputSpaceShort(processor));

            EmptyOutputSlot(inventory);
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(ProcessState.Idle, processor.CurrentState);
            Assert.IsFalse(ReadOutputSpaceShort(processor));
        }

        [Test]
        public void 入力が欠けると古い理由を残さない()
        {
            var recipe = ItemRecipe();
            var (_, inventory, processor) = PlaceSelected(recipe, Vector3Int.one);
            FillOutputSlot(recipe, inventory);
            InsertInputs(recipe, inventory);
            GameUpdater.UpdateOneTick();
            Assert.IsTrue(ReadOutputSpaceShort(processor));

            // 入力を抜いた次tickで、同じidleのまま理由が消えたことが通知される
            // On the tick after removing inputs, the cleared reason is notified while staying idle
            var notices = RecordReasonNotices(processor);
            EmptyInputs(inventory);
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(ProcessState.Idle, processor.CurrentState);
            Assert.IsFalse(ReadOutputSpaceShort(processor), "入力切れの待機は出力空き不足と区別される");
            // 機械更新の前に電気網の給電通知が旧値で届くので、そのtickの最後の通知で判定する
            // The electric network's supply notice arrives with the old value before the machine update, so judge the tick's last notice
            Assert.IsFalse(notices.Last(), "理由の消失が通知されない");
        }

        [Test]
        public void ロード直後の初期スナップショットが次tickを待たずに理由を持つ()
        {
            var recipe = ItemRecipe();
            var (block, inventory, processor) = PlaceSelected(recipe, Vector3Int.one);
            FillOutputSlot(recipe, inventory);
            InsertInputs(recipe, inventory);
            GameUpdater.UpdateOneTick();
            Assert.IsTrue(ReadOutputSpaceShort(processor));

            var reloaded = ServerContext.BlockFactory.Load(block.BlockGuid, block.BlockInstanceId, SaveLoadJsonTestHelper.ThroughJson(block.GetSaveState()),
                new BlockPositionInfo(new Vector3Int(20, 0, 20), BlockDirection.North, Vector3Int.one));
            var restored = reloaded.GetComponent<VanillaMachineProcessorComponent>();
            Assert.AreEqual(ProcessState.Idle, restored.CurrentState);
            Assert.IsTrue(ReadOutputSpaceShort(restored));
        }

        [Test]
        public void 液体出力が出力タンクに入りきらない待機も同じ理由を出す()
        {
            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data.First(r => 0 < r.OutputFluids.Length);
            ServerContext.GetService<IGameUnlockStateDataController>().UnlockMachineRecipe(recipe.MachineRecipeGuid);
            var (_, inventory, processor) = PlaceSelected(recipe, Vector3Int.one);
            var inputTanks = GetInputFluidContainers(inventory);
            for (var i = 0; i < recipe.InputFluids.Length; i++) inputTanks[i].AddLiquid(new FluidStack(recipe.InputFluids[i].Amount, MasterHolder.FluidMaster.GetFluidId(recipe.InputFluids[i].FluidGuid)));
            InsertInputs(recipe, inventory);

            // 液体出口は満杯、アイテム出口は空
            // Keep the fluid output full and the item output empty
            var outputTank = MachineFluidTestUtil.GetOutputFluidContainers(inventory)[0];
            outputTank.AddLiquid(new FluidStack(outputTank.Capacity, MasterHolder.FluidMaster.GetFluidId(recipe.OutputFluids[0].FluidGuid)));
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(ProcessState.Idle, processor.CurrentState);
            Assert.IsTrue(ReadOutputSpaceShort(processor));
        }
    }
}
