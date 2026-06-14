using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.CleanRoom;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.CleanRoom;
using Game.Context;
using Game.EnergySystem;
using Game.Fluid;
using Game.PlayerInventory;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Microsoft.Extensions.DependencyInjection;
using Mooresmaster.Model.MachineRecipesModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class CleanRoomMachineTest
    {
        // 未プッシュ（初期値 InValidRoom=false）の専用機械は、電力があっても処理を開始しない
        // An unpushed (default InValidRoom=false) machine never starts even with power
        [Test]
        public void DefaultEffectHaltsMachineTest()
        {
            var (block, proc, _) = PlaceExposureMachineWithInputs();

            RunTicksWithPower(block, 10);

            // 初期値は最悪側なので Idle のまま（開始しない）
            // Default effect is worst-case, so it stays Idle
            Assert.AreEqual(ProcessState.Idle, proc.CurrentState);
        }

        // Valid＋MaxGrade=4 をプッシュすると稼働し、チップ Lv は 1..4 の範囲（EUV失敗時は出力なしも正）
        // With Valid + MaxGrade=4 pushed, the machine runs and emits chips in Lv1..4 (EUV fail = no chip is OK)
        [Test]
        public void ValidEffectRunsAndOutputsChipTest()
        {
            var (block, proc, receiver) = PlaceExposureMachineWithInputs();
            receiver.SetCleanRoomEffect(new CleanRoomEffect(true, 4, 0.0));

            RunUntilIdle(block, proc);

            foreach (var lv in CollectOutputChipLevels(block)) Assert.That(lv, Is.InRange(1, 4));
        }

        // MaxGrade=2 では複数サイクル回しても Lv2 を超えない（天井クランプ）
        // With MaxGrade=2, no cycle ever emits above Lv2
        [Test]
        public void OutputCeilingClampTest()
        {
            var (block, proc, receiver) = PlaceExposureMachineWithInputs(cycles: 20);
            receiver.SetCleanRoomEffect(new CleanRoomEffect(true, 2, 0.15));

            RunCycles(block, proc, 20);

            foreach (var lv in CollectOutputChipLevels(block))
                Assert.LessOrEqual(lv, 2);
        }

        // MaxGrade=0（Out 相当）は稼働するが出力を生成しない（不良扱い。サイレント Lv1 禁止）
        // MaxGrade=0 (Out) keeps running but emits nothing (no silent Lv1)
        [Test]
        public void MaxGradeZeroRunsButEmitsNothingTest()
        {
            var (block, proc, receiver) = PlaceExposureMachineWithInputs();
            receiver.SetCleanRoomEffect(new CleanRoomEffect(true, 0, 0.0));

            RunUntilIdle(block, proc);

            // 入力は消費される（稼働は継続）が、チップは1つも出ない
            // Inputs are consumed (machine keeps operating) but no chip is emitted
            Assert.IsEmpty(CollectOutputChipLevels(block));
        }

        // 処理中に Invalid 化すると進捗が凍結し、Valid に戻ると再開する（壊れない）
        // Turning Invalid mid-process freezes progress; restoring Valid resumes (nothing breaks)
        [Test]
        public void MidProcessInvalidFreezesAndResumesTest()
        {
            var (block, proc, receiver) = PlaceExposureMachineWithInputs();
            receiver.SetCleanRoomEffect(new CleanRoomEffect(true, 4, 0.0));

            RunTicksWithPower(block, 3);
            Assert.AreEqual(ProcessState.Processing, proc.CurrentState);

            receiver.SetCleanRoomEffect(new CleanRoomEffect(false, 0, 0.0));
            var frozen = proc.RemainingTicks;
            RunTicksWithPower(block, 10);
            Assert.AreEqual(frozen, proc.RemainingTicks); // 凍結
            Assert.AreEqual(ProcessState.Processing, proc.CurrentState); // 壊れない・Idle に落ちない

            receiver.SetCleanRoomEffect(new CleanRoomEffect(true, 4, 0.0));
            RunUntilIdle(block, proc); // 再開して完走
        }

        // 入力消費数 ＝ チップ出力数 ＋ EUV失敗数（決定的 seed で正確に一致）。サイレント消失ゼロ
        // Inputs consumed == chips emitted + EUV failures, exactly (deterministic). No silent loss.
        [Test]
        public void NoSilentOutputLossTest()
        {
            const int cycles = 30;
            var (block, proc, receiver) = PlaceExposureMachineWithInputs(cycles: cycles);
            receiver.SetCleanRoomEffect(new CleanRoomEffect(true, 3, 0.35)); // down-bin 多発条件

            RunCycles(block, proc, cycles);

            // 専用機械側が数えた EUV 失敗数（テスト可視カウンタ）＋出力チップ数 ＝ サイクル数
            // Failures counted by the machine + emitted chips must equal completed cycles
            var emitted = CollectOutputChipLevels(block).Count;
            Assert.AreEqual(cycles, emitted + proc.EuvFailCountForTest);
        }

        // 出力スロットが残り1つの状態でレベル付きチップと副産物の両方が入らないなら開始を拒否する（副産物のサイレント消失を防ぐ）
        // With only one empty output slot, a leveled chip + by-product cannot both be stored -> must refuse to start (prevents silent by-product loss)
        [Test]
        public void OutputReservationRejectsWhenLeveledAndByProductCannotBothFitTest()
        {
            var (block, _, receiver) = PlaceExposureMachineWithInputs();
            receiver.SetCleanRoomEffect(new CleanRoomEffect(true, 4, 0.0));

            var recipe = FindExposureRecipe();
            var output = GetOutputInventory(block);

            // 露光レシピは「レベル付きチップ＋副産物」の2出力要素。確認しておく。
            // The exposure recipe must have two output elements (a leveled chip and a non-leveled by-product).
            Assert.AreEqual(2, recipe.OutputItems.Length, "test premise: exposure recipe has a leveled chip + a by-product");

            // 空きスロットを1つだけ残し、残りはチップで埋める（副産物が追記できる空きスタックを作らない）。
            // Leave exactly one empty slot, filling the rest with chips so the by-product cannot append anywhere.
            var chipId = MasterHolder.SemiconductorChipMaster.GetChipItemId(4);
            for (var i = 0; i < output.OutputSlot.Count - 1; i++)
                output.SetItem(i, ServerContext.ItemStackFactory.Create(chipId, 1));

            // 空き1つにはチップと副産物の両方は入らない。開始拒否でなければ副産物がサイレント消失する。
            // One empty slot cannot hold both; if it does not refuse, the by-product is silently dropped.
            Assert.IsFalse(output.IsAllowedToOutputItem(recipe),
                "must refuse to start when the leveled chip and the by-product cannot both be stored");
        }

        // 副産物（分布を持たない出力要素）はベース ItemId のまま（チップに化けない）
        // By-products (outputs without a distribution) keep their base ItemId
        [Test]
        public void ByProductNotReplacedTest()
        {
            var (block, proc, receiver) = PlaceExposureMachineWithInputs();
            receiver.SetCleanRoomEffect(new CleanRoomEffect(true, 4, 0.0));

            RunUntilIdle(block, proc);

            // 露光レシピの副産物出力（テスト mod で定義）がそのままの ItemId で存在する
            // The recipe's by-product output remains with its own ItemId
            AssertByProductPresent(block);
        }


        // 流体レシピを完走させ、入力流体が消費され出力流体が生成されることを確認する
        // Run the fluid recipe to completion; input fluid is consumed and output fluid is produced
        [Test]
        public void FluidRecipeConsumesAndProducesFluidTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 部屋外プッシュで上書きされないよう毎tickプッシュを止める（他テストと同じ）
            // Stop the per-tick push so the manually injected effect survives (same as other tests)
            serviceProvider.GetService<CleanRoomDatastore>().Destroy();

            var recipe = FindFluidRecipe();
            var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, Vector3Int.one, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);

            var inventory = block.GetComponent<CleanRoomMachineBlockInventoryComponent>();
            var receiver = (CleanRoomStateReceiverComponent)block.GetComponent<ICleanRoomStateReceiver>();
            var proc = block.GetComponent<CleanRoomMachineProcessorComponent>();

            // 室内有効＋出力レベル不問のエフェクトを与える
            // Inject a Valid-room effect so processing can start
            receiver.SetCleanRoomEffect(new CleanRoomEffect(true, 4, 0.0));

            // レシピのアイテム入力を投入
            // Insert the recipe's item inputs
            foreach (var inputItem in recipe.InputItems)
                inventory.InsertItem(ServerContext.ItemStackFactory.Create(inputItem.ItemGuid, inputItem.Count));

            // レシピの入力流体を入力タンクへ直接注入
            // Inject the recipe's input fluids directly into the input tank
            var inputTanks = GetInputFluidContainers(inventory);
            for (var i = 0; i < recipe.InputFluids.Length; i++)
            {
                var fluidId = MasterHolder.FluidMaster.GetFluidId(recipe.InputFluids[i].FluidGuid);
                inputTanks[i].AddLiquid(new FluidStack(recipe.InputFluids[i].Amount, fluidId), FluidContainer.Empty);
            }

            RunUntilIdle(block, proc);

            // 入力流体が消費されている（空にリセット）
            // Input fluid has been consumed (reset to empty)
            for (var i = 0; i < recipe.InputFluids.Length; i++)
            {
                Assert.AreEqual(0, inputTanks[i].Amount, 0.01, $"input fluid {i} not consumed");
                Assert.AreEqual(FluidMaster.EmptyFluidId, inputTanks[i].FluidId, $"input fluid {i} id not reset");
            }

            // 出力流体が生成されている
            // Output fluid has been produced
            var outputTanks = GetOutputFluidContainers(inventory);
            for (var i = 0; i < recipe.OutputFluids.Length; i++)
            {
                var expectedId = MasterHolder.FluidMaster.GetFluidId(recipe.OutputFluids[i].FluidGuid);
                Assert.AreEqual(expectedId, outputTanks[i].FluidId, $"output fluid {i} id mismatch");
                Assert.AreEqual(recipe.OutputFluids[i].Amount, outputTanks[i].Amount, 0.01, $"output fluid {i} amount mismatch");
            }
        }

        // タンクに流体をバッファした状態でセーブ→ロードし、流体が復元されることを確認する（Codexのsave-restore欠落の回帰固定）
        // Save with buffered fluid in the tanks, reload, and verify fluid is restored (pins the Codex save-restore gap)
        [Test]
        public void FluidSaveLoadRoundTripTest()
        {
            var fluidId1 = MasterHolder.FluidMaster.GetFluidId(new("00000000-0000-0000-1234-000000000001"));
            var fluidId2 = MasterHolder.FluidMaster.GetFluidId(new("00000000-0000-0000-1234-000000000002"));
            var blockGuid = MasterHolder.MachineRecipesMaster.MachineRecipes.Data
                .First(r => r.BlockGuid != Guid.Empty && IsCleanRoomMachineBlock(r.BlockGuid)).BlockGuid;
            var blockId = MasterHolder.BlockMaster.GetBlockId(blockGuid);

            // セーブ側のワールドを構築し、タンクに流体をバッファする
            // Build the save-side world and buffer fluid into the tanks
            var (_, saveProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var assembleSaveJsonText = saveProvider.GetService<AssembleSaveJsonText>();

            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            var inventory = block.GetComponent<CleanRoomMachineBlockInventoryComponent>();

            var inputTanks = GetInputFluidContainers(inventory);
            inputTanks[0].FluidId = fluidId1;
            inputTanks[0].Amount = 33.5;

            var outputTanks = GetOutputFluidContainers(inventory);
            outputTanks[0].FluidId = fluidId2;
            outputTanks[0].Amount = 17.25;

            var json = assembleSaveJsonText.AssembleSaveJson();
            ServerContext.WorldBlockDatastore.RemoveBlock(Vector3Int.zero, BlockRemoveReason.ManualRemove);

            // ロード側のワールドへ読み込んで流体が復元されることを確認する
            // Load into a fresh world and verify the fluid is restored
            var (_, loadProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var loader = loadProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson;
            loader.Load(json);

            var loadBlock = ServerContext.WorldBlockDatastore.GetBlock(Vector3Int.zero);
            var loadInventory = loadBlock.GetComponent<CleanRoomMachineBlockInventoryComponent>();

            var loadInput = GetInputFluidContainers(loadInventory);
            Assert.AreEqual(fluidId1, loadInput[0].FluidId, "input fluid id not restored");
            Assert.AreEqual(33.5, loadInput[0].Amount, 0.01, "input fluid amount not restored");

            var loadOutput = GetOutputFluidContainers(loadInventory);
            Assert.AreEqual(fluidId2, loadOutput[0].FluidId, "output fluid id not restored");
            Assert.AreEqual(17.25, loadOutput[0].Amount, 0.01, "output fluid amount not restored");
        }


        // 上限tick数。1サイクル分の安全余裕を取りつつ無限ループを防ぐ。
        // Upper tick bound; prevents infinite loops while leaving cycle headroom.
        private const int MaxTicksPerCycle = 2000;

        // 露光装置を設置し、cycles 回分の入力を投入する
        // Place the exposure machine and load inputs for the given cycles
        (IBlock block, CleanRoomMachineProcessorComponent proc, CleanRoomStateReceiverComponent receiver)
            PlaceExposureMachineWithInputs(int cycles = 1)
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 機械単体の挙動検証なので、データストアの毎tickプッシュ（部屋外＝最悪側で上書き）を止めて手動注入を活かす。
            // These are machine-component tests; stop the datastore's per-tick push (which would clobber the
            // manually injected effect with the worst case for a room-less machine) so injection stays in effect.
            serviceProvider.GetService<CleanRoomDatastore>().Destroy();

            var recipe = FindExposureRecipe();
            var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, Vector3Int.one, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);

            var inventory = block.GetComponent<CleanRoomMachineBlockInventoryComponent>();
            foreach (var inputItem in recipe.InputItems)
                inventory.InsertItem(ServerContext.ItemStackFactory.Create(inputItem.ItemGuid, inputItem.Count * cycles));

            return (block, block.GetComponent<CleanRoomMachineProcessorComponent>(),
                (CleanRoomStateReceiverComponent)block.GetComponent<ICleanRoomStateReceiver>());
        }

        // 毎tick給電しながら指定 tick 進める（既存 MachineIOTest 流儀。1回給電では完走しない）
        // Advance ticks while supplying power EVERY tick (MachineIOTest style)
        void RunTicksWithPower(IBlock block, int ticks)
        {
            var electric = block.GetComponent<CleanRoomMachineElectricComponent>();
            for (var i = 0; i < ticks; i++)
            {
                electric.SupplyEnergy(new ElectricPower(10000));
                GameUpdater.UpdateOneTick();
            }
        }

        // Idle まで毎tick給電で回す（上限tick付きで無限ループを防ぐ）
        // Run with power each tick until Idle (bounded to avoid hangs)
        void RunUntilIdle(IBlock block, CleanRoomMachineProcessorComponent proc)
        {
            var electric = block.GetComponent<CleanRoomMachineElectricComponent>();

            // まず1tick回して開始させる（Idle 即終了の誤検知を防ぐ）
            // Tick once first so processing starts (avoids a false "already Idle")
            for (var i = 0; i < MaxTicksPerCycle; i++)
            {
                electric.SupplyEnergy(new ElectricPower(10000));
                GameUpdater.UpdateOneTick();
                if (i > 0 && proc.CurrentState == ProcessState.Idle) return;
            }

            Assert.Fail("RunUntilIdle exceeded the tick budget");
        }

        // 全入力消費まで毎tick給電（cycles 回分の入力が尽きると Idle に留まる）
        // Supply power each tick until all inputs are consumed (cycles worth)
        void RunCycles(IBlock block, CleanRoomMachineProcessorComponent proc, int cycles)
        {
            var electric = block.GetComponent<CleanRoomMachineElectricComponent>();
            var idleStreak = 0;
            for (var i = 0; i < MaxTicksPerCycle * (cycles + 1); i++)
            {
                electric.SupplyEnergy(new ElectricPower(10000));
                GameUpdater.UpdateOneTick();

                // 入力が尽きると Idle のまま遷移しない。Idle が続いたら完了とみなす
                // Once inputs run out the machine stays Idle; treat a sustained Idle as done
                if (proc.CurrentState == ProcessState.Idle)
                {
                    idleStreak++;
                    if (idleStreak >= 3) return;
                }
                else
                {
                    idleStreak = 0;
                }
            }

            Assert.Fail("RunCycles exceeded the tick budget");
        }

        // 統合インベントリを GetChipLevel で走査し、レベル≥1 のチップだけ収集する（チップは入力/モジュールと衝突しない）
        // Scan the unified inventory; collect chip levels (>=1) only (chips never collide with input/module items)
        System.Collections.Generic.List<int> CollectOutputChipLevels(IBlock block)
        {
            var inventory = block.GetComponent<CleanRoomMachineBlockInventoryComponent>();
            var levels = new List<int>();
            foreach (var item in inventory.InventoryItems)
            {
                if (item.Id == ItemMaster.EmptyItemId) continue;
                var level = MasterHolder.SemiconductorChipMaster.GetChipLevel(item.Id);
                if (level < 1) continue;

                // 同 ItemId スロットに複数スタックされている場合も枚数分カウントする
                // Count each unit even when several share one slot stack
                var count = item.Count;
                for (var i = 0; i < count; i++) levels.Add(level);
            }
            return levels;
        }

        // 露光レシピの副産物出力（分布なし要素）が出力に存在することを確認する
        // Assert that the recipe's by-product output (non-leveled element) is present
        void AssertByProductPresent(IBlock block)
        {
            var recipe = FindExposureRecipe();
            var inventory = block.GetComponent<CleanRoomMachineBlockInventoryComponent>();

            // 分布を持たない出力要素＝副産物。その ItemId がそのまま出力に残っているはず
            // The non-distribution output element is the by-product; its ItemId must remain as-is
            foreach (var o in recipe.OutputItems)
            {
                if (MasterHolder.SemiconductorChipMaster.TryGetDistribution(recipe.MachineRecipeGuid, o.ItemGuid, out _)) continue;

                var byProductId = MasterHolder.ItemMaster.GetItemId(o.ItemGuid);
                var present = inventory.InventoryItems.Any(item => item.Id == byProductId && item.Count > 0);
                Assert.IsTrue(present, $"by-product {byProductId.AsPrimitive()} not found in output");
            }
        }

        Mooresmaster.Model.MachineRecipesModule.MachineRecipeMasterElement FindExposureRecipe()
        {
            // semiconductorChips マスタに分布を持つレシピ＝露光レシピを引く
            // The exposure recipe is the one with a distribution entry in the semiconductorChips master
            foreach (var r in MasterHolder.MachineRecipesMaster.MachineRecipes.Data)
            foreach (var o in r.OutputItems)
                if (MasterHolder.SemiconductorChipMaster.TryGetDistribution(r.MachineRecipeGuid, o.ItemGuid, out _)) return r;
            throw new Exception("exposure recipe not found");
        }

        // CleanRoomMachine 用かつ入出力流体を持つレシピを引く
        // Find the CleanRoomMachine recipe that uses input/output fluids
        MachineRecipeMasterElement FindFluidRecipe()
        {
            foreach (var r in MasterHolder.MachineRecipesMaster.MachineRecipes.Data)
            {
                if (!IsCleanRoomMachineBlock(r.BlockGuid)) continue;
                if (r.InputFluids.Length > 0 && r.OutputFluids.Length > 0) return r;
            }
            throw new Exception("CleanRoom fluid recipe not found");
        }

        bool IsCleanRoomMachineBlock(Guid blockGuid)
        {
            if (blockGuid == Guid.Empty) return false;
            var master = MasterHolder.BlockMaster.GetBlockMaster(blockGuid);
            return master.BlockType == "CleanRoomMachine";
        }

        // 専用ブロックインベントリから入出力流体タンクをリフレクションで取得する
        // Read the input/output fluid tanks from the dedicated block inventory via reflection
        IReadOnlyList<FluidContainer> GetInputFluidContainers(CleanRoomMachineBlockInventoryComponent inventory)
        {
            var input = (VanillaMachineInputInventory)typeof(CleanRoomMachineBlockInventoryComponent)
                .GetField("_inputInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(inventory);
            return input.FluidInputSlot;
        }

        IReadOnlyList<FluidContainer> GetOutputFluidContainers(CleanRoomMachineBlockInventoryComponent inventory)
        {
            return GetOutputInventoryFrom(inventory).FluidOutputSlot;
        }

        // 専用ブロックインベントリから出力インベントリ本体をリフレクションで取得する
        // Read the dedicated output inventory from the block inventory via reflection
        CleanRoomMachineOutputInventory GetOutputInventory(IBlock block)
        {
            return GetOutputInventoryFrom(block.GetComponent<CleanRoomMachineBlockInventoryComponent>());
        }

        CleanRoomMachineOutputInventory GetOutputInventoryFrom(CleanRoomMachineBlockInventoryComponent inventory)
        {
            return (CleanRoomMachineOutputInventory)typeof(CleanRoomMachineBlockInventoryComponent)
                .GetField("_outputInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(inventory);
        }

    }
}
