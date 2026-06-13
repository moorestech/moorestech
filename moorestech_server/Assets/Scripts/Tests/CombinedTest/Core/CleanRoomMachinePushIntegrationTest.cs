using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.CleanRoom;
using Game.Block.Blocks.Machine;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.CleanRoom;
using Game.CleanRoom.Machine;
using Game.Context;
using Game.EnergySystem;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    // フェーズ4 Task 5/6: CleanRoomDatastore が実部屋の効果を専用機械へプッシュし、機械が稼働/停止/天井クランプする end-to-end 検証。
    // Phase-4 Task 5/6: end-to-end verification that the datastore pushes the real room's effect to the dedicated machine.
    public class CleanRoomMachinePushIntegrationTest
    {
        // 基準部屋座標（内寸 5x5x3 を [0..6]x[0..4]x[0..6] の外殻で囲う。CleanRoomPollutionTest と同一ジオメトリ）。
        // Reference-room coordinates (5x5x3 cavity inside a [0..6]x[0..4]x[0..6] shell; same geometry as CleanRoomPollutionTest).
        private static readonly Vector3Int ShellMin = new(0, 0, 0);
        private static readonly Vector3Int ShellMax = new(6, 4, 6);
        private static readonly Vector3Int ItemHatchPos = new(0, 2, 2);
        private static readonly Vector3Int PipeHatchPos = new(6, 2, 2);
        private static readonly Vector3Int AirFilterPos = new(3, 1, 3);

        // 機械の設置セル（内部・側壁非接・フィルターと別セル）。1x1x1 機械なので 1 セル占有。
        // Machine cell (interior, not touching side walls, distinct from the filter). 1x1x1 machine -> one cell.
        private static readonly Vector3Int MachinePos = new(2, 2, 2);
        private static readonly Vector3Int InsideEmptyCellPos = new(1, 1, 1);

        // 電柱+発電機（フィルター給電用。機械は要求 10000W のため別途毎tick直接給電する）。
        // Pole + generator (powers the filter; the 10000W machine is supplied directly each tick).
        private static readonly Vector3Int PolePos = new(3, -3, 3);
        private static readonly Vector3Int GeneratorPos = new(4, -1, 3);

        private const float MachinePower = 10000f;
        private const int MaxTicks = 8000;

        // 密閉室内の機械へ Datastore が効果をプッシュし、解決された MaxGrade で稼働開始する。
        // The datastore pushes the resolved effect to a machine inside a sealed room; the machine starts.
        [Test]
        public void DatastorePushesEffectToMachineTest()
        {
            var ctx = SetUp();
            BuildReferenceRoom(ctx.World);
            PlacePoleAndInfinityGenerator(ctx.World);
            PlaceAirFilter(ctx.World);
            var (block, proc, receiver) = PlaceMachineWithInputs(ctx.World, cycles: 5);

            // 1サイクル分だけ回し、プッシュ→稼働開始を捉える（複数サイクルあるので消費しきらない）。
            // Run roughly one cycle so we catch push -> start (5 cycles of input remain).
            RunTicks(block, 30);

            // Datastore が機械の属する部屋を特定し、Valid＋MaxGrade>0 をプッシュ済みであること。
            // The datastore resolved the machine's room and pushed Valid + MaxGrade>0.
            Assert.IsTrue(ctx.Datastore.TryGetCleanRoom(block, out var room), "machine belongs to a single room");
            var expected = CleanRoomEffectResolver.Resolve(room);
            Assert.IsTrue(receiver.CurrentEffect.InValidRoom, "pushed InValidRoom=true");
            Assert.AreEqual(expected.MaxGrade, receiver.CurrentEffect.MaxGrade, "pushed MaxGrade matches resolver");
            Assert.Greater(receiver.CurrentEffect.MaxGrade, 0, "valid row has a positive ceiling");

            // 効果プッシュで機械が稼働開始している（30tick=約半サイクルなので Processing 中のはず）。
            // The push let the machine start; at 30 ticks (~half a 3s cycle) it should be Processing.
            Assert.AreEqual(ProcessState.Processing, proc.CurrentState, "machine started after the valid push");

            // さらに回して出力チップが実際に出ること（end-to-end 稼働の確証）。
            // Run more and confirm chips actually come out (end-to-end proof of operation).
            RunCycles(block, proc, 5);
            Assert.IsNotEmpty(CollectOutputChipLevels(block), "machine produced chips under the pushed valid effect");
        }

        // 基準部屋（エアフィルター1台・平衡 C≈2.77＝A 行）→ 機械の出力チップは Lv ≤ 4（A 行 MaxGrade=4 の天井）。
        // Reference room (one filter, C_eq≈2.77 = row A) -> output chips are Lv <= 4 (row-A ceiling = MaxGrade 4).
        [Test]
        public void RealRoomRowAOutputsUpToLv4Test()
        {
            var ctx = SetUp();
            BuildReferenceRoom(ctx.World);
            PlacePoleAndInfinityGenerator(ctx.World);
            PlaceAirFilter(ctx.World);
            var (block, proc, _) = PlaceMachineWithInputs(ctx.World, cycles: 20);

            // 平衡到達まで給電（τ≈14.8s。十分回して A 行へ収束させる）。
            // Power to equilibrium (τ≈14.8s); converge to row A.
            RunTicks(block, 400);
            Assert.IsTrue(ctx.Datastore.TryGetCleanRoomAt(InsideEmptyCellPos, out var room));
            Assert.AreEqual(0, room.ThresholdIndex, "equilibrated to row A (real equilibrium, no aid)");

            RunCycles(block, proc, 20);

            var levels = CollectOutputChipLevels(block);
            Assert.IsNotEmpty(levels, "row A produces chips");
            foreach (var lv in levels) Assert.LessOrEqual(lv, 4, "row-A ceiling is Lv4");
        }

        // 汚い部屋（汚染レートを高くし平衡を C 行へ）→ 出力は Lv ≤ 2（天井クランプ）。実平衡で C 行に着地する。
        // A dirtier room (higher pollution -> equilibrium on row C) -> outputs <= Lv2. Lands on row C via real equilibrium.
        [Test]
        public void RealRoomRowCNeverExceedsLv2Test()
        {
            var ctx = SetUp();

            // 決定論補助（汚染源のみ）: 機械汚染/ハッチ搬送の実供給はフェーズ5。ここは A_total を一定値で注入し、
            // C_eq = A_total/(n·q) = 300/5 = 60 ∈ (50,200] ＝ C 行へ実積分で収束させる（行・天井は本物）。
            // Determinism aid (pollution only): phase-5 supplies real machine/hatch pollution; here we inject a constant
            // A_total so C_eq = 300/5 = 60 lands in row C by real integration (the row and the ceiling stay genuine).
            ctx.Datastore.SetPollutionPerSecondProvider(_ => 300.0);

            BuildReferenceRoom(ctx.World);
            PlacePoleAndInfinityGenerator(ctx.World);

            // フィルター100個投入（容量100×5000=500000）。平衡到達まで摩耗(≈A_total·t≈45000)で消費しきらず nq=5 を維持する。
            // Load 100 filters (capacity 500000) so wear (~A_total·t≈45000) never empties the bank; nq stays 5.
            PlaceAirFilterStack(ctx.World, 100);

            // 機械は設置のみ（入力は投入しない）。平衡到達前に稼働して上位行の高 Lv チップを混ぜないため。
            // Place the machine WITHOUT inputs so it cannot run (and emit higher-row chips) before equilibrium settles.
            var (block, proc, receiver) = PlaceMachineAt(ctx.World, MachinePos, cycles: 0);

            // τ=V/(nq)=74/5≈14.8s → 150s(3000tick≈10τ)で C 行平衡へ収束。
            // τ≈14.8s; 3000 ticks (~10τ) converge to the row-C equilibrium.
            RunTicks(block, 3000);
            Assert.IsTrue(ctx.Datastore.TryGetCleanRoomAt(InsideEmptyCellPos, out var room));
            Assert.AreEqual(2, room.ThresholdIndex, "equilibrated to row C (real equilibrium)");
            Assert.AreEqual(2, receiver.CurrentEffect.MaxGrade, "pushed row-C ceiling = MaxGrade 2");

            // 平衡後（行が C で安定してから）に入力を投入して稼働させる。
            // Load inputs only after equilibrium (row C is stable) so every emitted chip is clamped to Lv2.
            LoadMachineInputs(block, cycles: 20);
            RunCycles(block, proc, 20);

            var levels = CollectOutputChipLevels(block);
            Assert.IsNotEmpty(levels, "row C still produces chips");
            foreach (var lv in levels) Assert.LessOrEqual(lv, 2, "row-C ceiling is Lv2");
        }

        // 壁破壊で密閉が消滅すると最悪側プッシュで機械が凍結（Processing 維持・RemainingTicks 不変）。
        // 猶予内の再封なら部屋が同一性を保って復活し、Valid プッシュで稼働再開する（両側を検証）。
        // Breaking a wall vanishes the room -> worst push -> the machine freezes (Processing, RemainingTicks fixed).
        // Resealing within grace revives the room (same identity) -> Valid push resumes operation. Both sides asserted.
        [Test]
        public void RealRoomInvalidAfterGraceHaltsMachineTest()
        {
            var breachWall = new Vector3Int(0, 2, 3);

            // --- サブシナリオ1: 破壊したまま回すと進捗が凍結し続ける ---
            // --- Sub-scenario 1: left breached -> progress stays frozen ---
            {
                var ctx = SetUp();
                BuildReferenceRoom(ctx.World);
                PlacePoleAndInfinityGenerator(ctx.World);
                PlaceAirFilter(ctx.World);
                var (block, proc, receiver) = PlaceMachineWithInputs(ctx.World, cycles: 5);

                RunTicks(block, 30);
                Assert.AreEqual(ProcessState.Processing, proc.CurrentState, "machine processing before breach");

                // 境界壁を1枚破壊して密閉を破る（部屋が消滅し最悪側プッシュ）。
                // Break one boundary wall (room vanishes -> worst push).
                BreakWall(ctx.World, breachWall);
                RunTicks(block, 20); // 消滅検出＋最悪側プッシュが行き渡るまで / let vanish + worst push settle

                // 凍結を確認: 最悪側プッシュ後は Processing のまま進捗が止まる。
                // Verify freeze: worst push keeps it Processing while progress halts.
                Assert.IsFalse(receiver.CurrentEffect.InValidRoom, "worst push after vanish");
                var frozen = proc.RemainingTicks;
                RunTicks(block, 150); // 猶予(100tick)超を含めて十分回す / well past the 100-tick grace
                Assert.AreEqual(frozen, proc.RemainingTicks, "progress frozen while breached");
                Assert.AreEqual(ProcessState.Processing, proc.CurrentState, "stays Processing (frozen, not Idle)");
            }

            // --- サブシナリオ2: 猶予内の再封で稼働再開 ---
            // --- Sub-scenario 2: resealed within grace -> resumes ---
            {
                var ctx = SetUp();
                BuildReferenceRoom(ctx.World);
                PlacePoleAndInfinityGenerator(ctx.World);
                PlaceAirFilter(ctx.World);
                var (block, proc, receiver) = PlaceMachineWithInputs(ctx.World, cycles: 5);

                RunTicks(block, 30);
                Assert.AreEqual(ProcessState.Processing, proc.CurrentState);

                // 壁を壊して凍結 → 猶予内（<100tick）に再封 → 部屋復活 → Valid プッシュで再開。
                // Break (freeze) -> reseal within grace (<100 ticks) -> room revives -> Valid push resumes.
                BreakWall(ctx.World, breachWall);
                RunTicks(block, 20);
                var frozen = proc.RemainingTicks;
                Assert.IsFalse(receiver.CurrentEffect.InValidRoom, "frozen while breached");

                ResealWall(ctx.World, breachWall);
                RunTicks(block, 40); // < 100-tick grace から十分余裕 / well within the grace window

                // 再封で Valid に戻り、進捗が凍結値から進む（＝稼働再開。Idle に落ちていない）。
                // Reseal restores Valid and progress moves off the frozen value (resumed; not dropped to Idle).
                Assert.IsTrue(receiver.CurrentEffect.InValidRoom, "valid push after reseal");
                Assert.AreEqual(ProcessState.Processing, proc.CurrentState, "resealed within grace keeps running");
                Assert.AreNotEqual(frozen, proc.RemainingTicks, "progress advanced after reseal");
            }
        }

        // 室境界をまたぐ multi-block 機械（一部が部屋外）→ Datastore が InValidRoom=false をプッシュ→停止。
        // A straddling machine (partly outside the room) -> datastore pushes InValidRoom=false -> halts.
        [Test]
        public void RealRoomStraddlingMachineHaltsTest()
        {
            var ctx = SetUp();
            BuildReferenceRoom(ctx.World);
            PlacePoleAndInfinityGenerator(ctx.World);
            PlaceAirFilter(ctx.World);

            // 機械を部屋の外（どの部屋の Cells にも含まれない遠方の空セル）に置く＝占有セルが部屋外。
            // 1x1x1 機械は単一セルなので「またがり」＝部屋外配置で再現する（占有セルが部屋に属さない）。
            // Place the machine fully outside any room (a far exterior empty cell); for a 1x1x1 block this is the
            // straddling/outside case (its occupied cell belongs to no room's Cells).
            var (block, proc, receiver) = PlaceMachineAt(ctx.World, new Vector3Int(20, 1, 20), cycles: 1);

            RunTicks(block, 50);

            // 占有セルが単一部屋に含まれない → TryGetCleanRoom=false → 最悪側プッシュ。
            // Occupied cell is not in a single room -> TryGetCleanRoom=false -> worst push.
            Assert.IsFalse(ctx.Datastore.TryGetCleanRoom(block, out _), "machine is not inside one room");
            Assert.IsFalse(receiver.CurrentEffect.InValidRoom, "pushed InValidRoom=false");
            Assert.AreEqual(ProcessState.Idle, proc.CurrentState, "halts (stays Idle)");
        }

        #region Internal

        private sealed class Ctx
        {
            public IWorldBlockDatastore World;
            public CleanRoomDatastore Datastore;
        }

        // DI コンテナを生成し、ワールド＋クリーンルームデータストアを取り出す。
        // Create the DI container and pull the world + clean-room datastore.
        private static Ctx SetUp()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            return new Ctx
            {
                World = ServerContext.WorldBlockDatastore,
                Datastore = serviceProvider.GetService<CleanRoomDatastore>(),
            };
        }

        // 内寸 5x5x3 を壁で囲い、境界壁2枚を ItemHatch / PipeHatch に差し替える（接続点2・密閉維持）。
        // Seal a 5x5x3 cavity; swap two boundary walls for ItemHatch / PipeHatch (connectors=2, seal preserved).
        private static void BuildReferenceRoom(IWorldBlockDatastore world)
        {
            for (var x = ShellMin.x; x <= ShellMax.x; x++)
            for (var y = ShellMin.y; y <= ShellMax.y; y++)
            for (var z = ShellMin.z; z <= ShellMax.z; z++)
            {
                var onShell = x == ShellMin.x || x == ShellMax.x ||
                              y == ShellMin.y || y == ShellMax.y ||
                              z == ShellMin.z || z == ShellMax.z;
                if (!onShell) continue;

                var pos = new Vector3Int(x, y, z);
                if (pos == ItemHatchPos || pos == PipeHatchPos) continue;
                world.TryAddBlock(ForUnitTestModBlockId.CleanRoomWallId, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            }

            world.TryAddBlock(ForUnitTestModBlockId.CleanRoomItemHatchId, ItemHatchPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            world.TryAddBlock(ForUnitTestModBlockId.CleanRoomPipeHatchId, PipeHatchPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
        }

        private static void PlacePoleAndInfinityGenerator(IWorldBlockDatastore world)
        {
            world.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, PolePos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            world.TryAddBlock(ForUnitTestModBlockId.InfinityGeneratorId, GeneratorPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
        }

        private static void PlaceAirFilter(IWorldBlockDatastore world)
        {
            PlaceAirFilterStack(world, 5);
        }

        // エアフィルター1台を置き、フィルターアイテムを指定個数投入する（摩耗で消費される）。
        // Place one air filter and load the given number of filter items (consumed by wear).
        private static void PlaceAirFilterStack(IWorldBlockDatastore world, int filterCount)
        {
            world.TryAddBlock(ForUnitTestModBlockId.CleanRoomAirFilterId, AirFilterPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var filterBlock);
            filterBlock.GetComponent<CleanRoomAirFilterItemComponent>()
                .InsertItem(ServerContext.ItemStackFactory.Create(ForUnitTestModBlockId.CleanRoomFilterItemGuid, filterCount));
        }

        // 露光レシピ（semiconductorChips に分布を持つレシピ）の機械をデフォルト位置に置き、入力を投入する。
        // Place the exposure-recipe machine at the default cell and load inputs.
        private static (IBlock block, CleanRoomMachineProcessorComponent proc, CleanRoomStateReceiverComponent receiver)
            PlaceMachineWithInputs(IWorldBlockDatastore world, int cycles)
        {
            return PlaceMachineAt(world, MachinePos, cycles);
        }

        private static (IBlock block, CleanRoomMachineProcessorComponent proc, CleanRoomStateReceiverComponent receiver)
            PlaceMachineAt(IWorldBlockDatastore world, Vector3Int pos, int cycles)
        {
            var recipe = FindExposureRecipe();
            var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            world.TryAddBlock(blockId, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);

            if (cycles > 0) LoadMachineInputs(block, cycles);

            return (block, block.GetComponent<CleanRoomMachineProcessorComponent>(),
                (CleanRoomStateReceiverComponent)block.GetComponent<ICleanRoomStateReceiver>());
        }

        // 露光レシピの入力アイテムを cycles 回分投入する。
        // Load the exposure recipe's input items for the given number of cycles.
        private static void LoadMachineInputs(IBlock block, int cycles)
        {
            var recipe = FindExposureRecipe();
            var inventory = block.GetComponent<CleanRoomMachineBlockInventoryComponent>();
            foreach (var inputItem in recipe.InputItems)
                inventory.InsertItem(ServerContext.ItemStackFactory.Create(inputItem.ItemGuid, inputItem.Count * cycles));
        }

        // 境界壁を破壊する（密閉を破り部屋消滅→猶予開始）。
        // Break a boundary wall (open the seal -> room vanishes -> grace starts).
        private static void BreakWall(IWorldBlockDatastore world, Vector3Int pos)
        {
            world.RemoveBlock(pos, BlockRemoveReason.ManualRemove);
        }

        // 同じセルに境界壁を置き直す（再封）。
        // Re-place a boundary wall at the same cell (reseal).
        private static void ResealWall(IWorldBlockDatastore world, Vector3Int pos)
        {
            world.TryAddBlock(ForUnitTestModBlockId.CleanRoomWallId, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
        }

        // 機械へ毎tick直接給電しつつ GameUpdater を進める（Datastore.Update のプッシュも毎tick走る）。
        // Supply the machine directly each tick while ticking GameUpdater (the datastore push runs each tick too).
        private static void RunTicks(IBlock block, int ticks)
        {
            var electric = block.GetComponent<CleanRoomMachineElectricComponent>();
            for (var i = 0; i < ticks; i++)
            {
                electric.SupplyEnergy(new ElectricPower(MachinePower));
                GameUpdater.UpdateOneTick();
            }
        }

        // 入力が尽きるまで（Idle 安定まで）毎tick給電で回す。
        // Run with power each tick until inputs run out (Idle stabilizes).
        private static void RunCycles(IBlock block, CleanRoomMachineProcessorComponent proc, int cycles)
        {
            var electric = block.GetComponent<CleanRoomMachineElectricComponent>();
            var idleStreak = 0;
            for (var i = 0; i < MaxTicks; i++)
            {
                electric.SupplyEnergy(new ElectricPower(MachinePower));
                GameUpdater.UpdateOneTick();
                if (proc.CurrentState == ProcessState.Idle)
                {
                    idleStreak++;
                    if (idleStreak >= 3) return;
                }
                else idleStreak = 0;
            }
            Assert.Fail("RunCycles exceeded the tick budget");
        }

        // 統合インベントリを走査し、チップレベル（≥1）だけ枚数分収集する。
        // Scan the unified inventory; collect chip levels (>=1), one per unit.
        private static List<int> CollectOutputChipLevels(IBlock block)
        {
            var inventory = block.GetComponent<CleanRoomMachineBlockInventoryComponent>();
            var levels = new List<int>();
            foreach (var item in inventory.InventoryItems)
            {
                if (item.Id == ItemMaster.EmptyItemId) continue;
                var level = MasterHolder.SemiconductorChipMaster.GetChipLevel(item.Id);
                if (level < 1) continue;
                for (var i = 0; i < item.Count; i++) levels.Add(level);
            }
            return levels;
        }

        private static Mooresmaster.Model.MachineRecipesModule.MachineRecipeMasterElement FindExposureRecipe()
        {
            foreach (var r in MasterHolder.MachineRecipesMaster.MachineRecipes.Data)
            foreach (var o in r.OutputItems)
                if (MasterHolder.SemiconductorChipMaster.TryGetDistribution(r.MachineRecipeGuid, o.ItemGuid, out _)) return r;
            throw new Exception("exposure recipe not found");
        }

        #endregion
    }
}
