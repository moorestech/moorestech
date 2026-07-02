using System;
using System.Collections.Generic;
using Core.Master;
using Core.Update;
using Newtonsoft.Json.Linq;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.CleanRoom;
using Game.Context;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class CleanRoomPuritySimulationTest
    {
        [Test]
        public void ThresholdMaster_LoadsFourRows_BestFirst()
        {
            // DIコンテナ生成で MasterHolder.Load が走る。
            // Creating the DI container loads MasterHolder.
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var master = MasterHolder.CleanRoomThresholdMaster;
            Assert.AreEqual(4, master.Rows.Count);
            Assert.AreEqual(4, master.OutThresholdIndex);

            // 行0が最良（A相当）。値はバランス確定書§1。
            // Row 0 is the cleanest tier; values from balance §1.
            Assert.AreEqual(10.0, master.Rows[0].MaxConcentration, 1e-9);
            Assert.AreEqual(0.0167, master.Rows[0].RequiredAirChangeRate, 1e-9);
            Assert.AreEqual(1000.0, master.Rows[3].MaxConcentration, 1e-9);
            Assert.AreEqual(0.0014, master.Rows[3].RequiredAirChangeRate, 1e-9);
        }

        // 有効なテストMODのマスタは Validate を通る（非回帰ガード）。
        // The valid test-mod master passes Validate (regression guard).
        [Test]
        public void ThresholdMaster_Validate_ValidMaster_Passes()
        {
            var master = new CleanRoomThresholdMaster(ValidThresholdJToken());
            var ok = master.Validate(out var msg);
            Assert.IsTrue(ok, msg);
            Assert.IsNull(msg);
        }

        // 空テーブルは機能opt-out（未提供Modのフォールバック）として Validate を通る。
        // An empty table passes Validate as a feature opt-out (fallback for mods without the file).
        [Test]
        public void ThresholdMaster_Validate_EmptyRows_PassesAsOptOut()
        {
            var token = ValidThresholdJToken();
            ((JArray)token["data"]).Clear();

            var master = new CleanRoomThresholdMaster(token);
            var ok = master.Validate(out var msg);
            Assert.IsTrue(ok, msg);
            Assert.AreEqual(0, master.OutThresholdIndex);
        }

        // downBinRate が [0,1] を外れると Validate が失敗する。
        // Validate fails when downBinRate is outside [0,1].
        [Test]
        public void ThresholdMaster_Validate_DownBinRateOutOfRange_Fails()
        {
            var token = ValidThresholdJToken();
            token["data"][0]["downBinRate"] = 1.5;

            AssertThresholdValidateFails(token);
        }

        // maxGrade が負だと Validate が失敗する。
        // Validate fails when maxGrade is negative.
        [Test]
        public void ThresholdMaster_Validate_NegativeMaxGrade_Fails()
        {
            var token = ValidThresholdJToken();
            token["data"][0]["maxGrade"] = -1;

            AssertThresholdValidateFails(token);
        }

        // Validate が false かつメッセージ非空であることを確認する共通アサート。
        // Shared assert: Validate returns false with a non-empty message.
        private static void AssertThresholdValidateFails(JToken token)
        {
            var master = new CleanRoomThresholdMaster(token);
            var ok = master.Validate(out var msg);
            Assert.IsFalse(ok);
            Assert.IsNotEmpty(msg);
        }

        // テストMOD相当の有効な JToken を生成する（テスト毎に独立コピー）。
        // Builds a valid test-mod-equivalent JToken (independent copy per test).
        private static JToken ValidThresholdJToken()
        {
            return JObject.Parse(@"
            {
              ""data"": [
                { ""label"": ""A"", ""maxConcentration"": 10.0,   ""maxGrade"": 4, ""downBinRate"": 0.0,  ""requiredAirChangeRate"": 0.0167 },
                { ""label"": ""B"", ""maxConcentration"": 50.0,   ""maxGrade"": 3, ""downBinRate"": 0.05, ""requiredAirChangeRate"": 0.0083 },
                { ""label"": ""C"", ""maxConcentration"": 200.0,  ""maxGrade"": 2, ""downBinRate"": 0.15, ""requiredAirChangeRate"": 0.0042 },
                { ""label"": ""D"", ""maxConcentration"": 1000.0, ""maxGrade"": 1, ""downBinRate"": 0.35, ""requiredAirChangeRate"": 0.0014 }
              ]
            }");
        }

        // バランス確定書§1 の4行（A/B/C/D相当）。判定純関数テスト用。
        // The four rows from balance §1, for pure decision tests.
        private static readonly CleanRoomThresholdRow[] Rows =
        {
            new CleanRoomThresholdRow(10.0, 0.0167),
            new CleanRoomThresholdRow(50.0, 0.0083),
            new CleanRoomThresholdRow(200.0, 0.0042),
            new CleanRoomThresholdRow(1000.0, 0.0014),
        };

        // 全行のACH要求（昇格マージン込み）を満たす十分大きい値。
        // ACH large enough to satisfy every row incl. promotion margin.
        private const double AchAllPass = 1.0;

        [Test]
        public void Decide_Row0_HoldBand_StaysRow0()
        {
            // 現在行0・C=9.5（保持帯 8〜10）→ 行0維持。C=11 で素閾値10超え → 行1へ降格。
            // Row 0 holds at C=9.5 (8..10 band); C=11 exceeds 10 -> demote to row 1.
            Assert.AreEqual(0, CleanRoomPurityRules.DecideThresholdIndex(0, 9.5, AchAllPass, Rows));
            Assert.AreEqual(1, CleanRoomPurityRules.DecideThresholdIndex(0, 11.0, AchAllPass, Rows));
        }

        [Test]
        public void Decide_Row1_PromotesOnlyAtOrBelowMargin()
        {
            // C=9（昇格境界 10×0.8=8 超）→ 行1維持。C=8.0（境界ちょうど）→ 行0へ昇格。
            // C=9 above the 8.0 promote bound stays row 1; C=8.0 promotes to row 0.
            Assert.AreEqual(1, CleanRoomPurityRules.DecideThresholdIndex(1, 9.0, AchAllPass, Rows));
            Assert.AreEqual(0, CleanRoomPurityRules.DecideThresholdIndex(1, 8.0, AchAllPass, Rows));
        }

        [Test]
        public void Decide_AchShortfall_Demotes()
        {
            // 現在行0・C=3.2 だが ACH=0.01 < 0.0167。行0→行1 は降格なので
            // 行1の素の要求 0.0083 を使い（昇格境界は無関係）、行1 に落ち着く。
            // Demotion from row 0 uses row 1's raw ACH requirement (0.0083), not promote bounds.
            Assert.AreEqual(1, CleanRoomPurityRules.DecideThresholdIndex(0, 3.2, 0.01, Rows));
        }

        [Test]
        public void Decide_AchPromotion_RequiresMargin()
        {
            // 行1から行0への昇格は ACH ≥ 0.0167×1.25 = 0.020875 が必要。
            // Promotion to row 0 needs ACH ≥ required × 1.25 (anti-flicker margin).
            Assert.AreEqual(1, CleanRoomPurityRules.DecideThresholdIndex(1, 5.0, 0.018, Rows));
            Assert.AreEqual(0, CleanRoomPurityRules.DecideThresholdIndex(1, 5.0, 0.021, Rows));
        }

        [Test]
        public void Decide_VeryDirtyOrFromOut_BehavesPerHysteresis()
        {
            // C=2000 は全行不成立 → Out（=rows.Count=4）。
            Assert.AreEqual(4, CleanRoomPurityRules.DecideThresholdIndex(0, 2000.0, AchAllPass, Rows));
            // Out(4)からの復帰は全行が昇格扱い: C=9 は行0(≤8)不成立・行1(≤40)成立 → 1。
            // Recovery from Out treats every row as promotion: C=9 fails row0 (≤8), meets row1 (≤40).
            Assert.AreEqual(1, CleanRoomPurityRules.DecideThresholdIndex(4, 9.0, AchAllPass, Rows));
        }

        [Test]
        public void Integrate_ConvergesToWorkedExampleEquilibrium()
        {
            // worked example: V=75, A_total=16, n·q=5 → N_eq=240（C_eq=3.2）。
            // 2000tick（100秒 ≒ 6.7τ, τ=15秒）回して平衡へ。
            // Balance §4 worked example; run 2000 ticks (≈6.7τ) to equilibrium.
            var n = 0.0;
            for (var i = 0; i < 2000; i++)
                n = CleanRoomPurityRules.IntegrateTick(n, 75.0, 16.0, 5.0, 0.05);

            Assert.AreEqual(240.0, n, 2.0, "N_eq = A_total/(n·q) · V = 240");
            Assert.AreEqual(3.2, n / 75.0, 0.05, "C_eq = 16/5 = 3.2");
        }

        [Test]
        public void Integrate_ClampsAtZero()
        {
            // 除去が過剰でも N は負にならない。
            // Over-removal never drives N negative.
            var n = CleanRoomPurityRules.IntegrateTick(1.0, 1.0, 0.0, 100.0, 0.05);
            Assert.AreEqual(0.0, n, 1e-9);
        }

        [Test]
        public void Room_AddRemoveImpurity_ClampsAtZero_AndConcentrationUsesVolume()
        {
            // 純度状態の最小単位テスト。N加減・0クランプ・濃度・ステータス設定を確認する。
            // Minimal purity-state test: add/remove, zero-clamp, concentration, and status setter.
            var cells = new HashSet<Vector3Int> { new Vector3Int(0, 0, 0), new Vector3Int(1, 0, 0) };
            var room = new CleanRoom(0, cells, 2, 10);

            room.AddImpurity(100.0);
            Assert.AreEqual(100.0, room.ImpurityCount, 1e-9);
            Assert.AreEqual(50.0, room.Concentration, 1e-9, "C = N/V = 100/2");

            room.RemoveImpurity(30.0);
            Assert.AreEqual(70.0, room.ImpurityCount, 1e-9);

            // 過剰除去は 0 でクランプ。
            // Over-removal clamps at zero.
            room.RemoveImpurity(1000.0);
            Assert.AreEqual(0.0, room.ImpurityCount, 1e-9);

            // 段階と猶予のセッター。
            // Status and grace setter.
            room.SetStatus(CleanRoomRoomStatus.Degraded, 5.0);
            Assert.AreEqual(CleanRoomRoomStatus.Degraded, room.Status);
            Assert.AreEqual(5.0, room.GraceRemainingSeconds, 1e-9);

            room.SetThresholdIndex(2);
            Assert.AreEqual(2, room.ThresholdIndex);
        }

        [Test]
        public void Redistribute_SplitMergeExpand_ConserveCorrectly()
        {
            // 分割: 旧{Cells=10, N=100} → 新2部屋(各overlap=5) → 各50・総和100保存。
            // Split conserves total N; each part gets C_old·overlap.
            var n1 = CleanRoomPurityRules.RedistributeImpurity(100.0, 10, 5);
            var n2 = CleanRoomPurityRules.RedistributeImpurity(100.0, 10, 5);
            Assert.AreEqual(50.0, n1, 1e-9);
            Assert.AreEqual(100.0, n1 + n2, 1e-9);

            // 結合: 旧2部屋{Cells=5, N=50}が全セル重なりで1新部屋へ → 合算100。
            // Merge sums N.
            var merged = CleanRoomPurityRules.RedistributeImpurity(50.0, 5, 5)
                       + CleanRoomPurityRules.RedistributeImpurity(50.0, 5, 5);
            Assert.AreEqual(100.0, merged, 1e-9);

            // 拡張: 旧{Cells=27, N=100}を全て含む大部屋（overlap=27）→ N=100保存（濃度は希釈）。
            // Expansion preserves N; new cells are clean air (dilution).
            Assert.AreEqual(100.0, CleanRoomPurityRules.RedistributeImpurity(100.0, 27, 27), 1e-9);

            // 縮小: overlap=10/27 → N按分（残り17セル分のNは消滅）。
            // Shrink keeps concentration; N outside the overlap vanishes.
            Assert.AreEqual(100.0 * 10 / 27, CleanRoomPurityRules.RedistributeImpurity(100.0, 27, 10), 1e-9);
        }

        // テスト用フィルタースタブ。固定 q を返す。
        // Test stub filter returning a fixed q.
        private sealed class AirFilterStub : ICleanRoomAirFilter
        {
            public double RemovalVolumePerSecond { get; }
            public bool IsDestroy { get; private set; }
            public AirFilterStub(double q) { RemovalVolumePerSecond = q; }
            public void Destroy() { IsDestroy = true; }
            public void ApplyRemovedImpurity(double removed) { }
        }

        [Test]
        public void Datastore_ReferenceRoom_ConvergesToCeq3p2_Row0()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var datastore = serviceProvider.GetService<CleanRoomDatastore>();

            // 外殻 7x7x5 → 内部 5x5x3 = V75。
            // Shell 7x7x5 -> interior 5x5x3 = V75.
            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(6, 6, 4));
            datastore.RebuildAll();
            Assert.AreEqual(1, datastore.Rooms.Count);
            Assert.AreEqual(75, datastore.Rooms[0].Volume, "Reference room must be V=75");

            // 室内セルに q=5 のフィルター1台＋A_total=16 を定数注入（バランス確定書§7の流儀）。
            // One filter q=5 at an interior cell; inject constant A_total=16 (balance §7).
            var insideCell = new Vector3Int(3, 2, 3);
            Assert.True(datastore.Rooms[0].Contains(insideCell));
            datastore.AddAirFilter(insideCell, new AirFilterStub(5.0));
            datastore.SetPollutionPerSecondProvider(_ => 16.0);

            GameUpdater.RunFrames(2000);

            // 再検出で部屋オブジェクトが入れ替わっている可能性があるため再取得。
            // Re-fetch: re-detection may have replaced the room instance.
            var room = datastore.Rooms[0];
            Assert.AreEqual(3.2, room.Concentration, 0.05, "C_eq = 16/5 = 3.2");
            Assert.AreEqual(0, room.ThresholdIndex, "best row (A) at equilibrium");
        }

        [Test]
        public void Datastore_NoFilter_FallsToOut_EvenFromBestRow()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var datastore = serviceProvider.GetService<CleanRoomDatastore>();

            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4)); // V27
            datastore.RebuildAll();
            var room = datastore.Rooms[0];

            // 非トートロジー化: いったん最良行0にセットし、「tickで Out へ落ちる」ことを検証する。
            // Anti-tautology: seed row 0 first, then assert one tick drops it to Out (ACH=0).
            room.SetThresholdIndex(0);
            datastore.SetPollutionPerSecondProvider(_ => 16.0);
            GameUpdater.RunFrames(1);

            var outIndex = MasterHolder.CleanRoomThresholdMaster.OutThresholdIndex;
            Assert.AreEqual(outIndex, datastore.Rooms[0].ThresholdIndex, "ACH=0 fails every row -> Out");
            // 積分も走っている（N = 16×0.05 = 0.8）。
            // Integration also ran (N accumulated one tick of A_total).
            Assert.AreEqual(0.8, datastore.Rooms[0].ImpurityCount, 1e-6);
        }

        [Test]
        public void Datastore_FreshRoom_StartsAtOut()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var datastore = serviceProvider.GetService<CleanRoomDatastore>();

            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));
            datastore.RebuildAll();

            // 新規部屋の初期行は Out（最良で生まれて保持帯の恩恵を受けてはならない）。
            // A fresh room must start at Out, not at the best row.
            var outIndex = MasterHolder.CleanRoomThresholdMaster.OutThresholdIndex;
            Assert.AreEqual(outIndex, datastore.Rooms[0].ThresholdIndex);
        }

        [Test]
        public void Datastore_SealBreak_KeepsImpurity_AndGoesDegraded()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var datastore = serviceProvider.GetService<CleanRoomDatastore>();
            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4)); // V27
            datastore.RebuildAll();

            datastore.Rooms[0].AddImpurity(150.0);

            // 壁を1枚壊して密閉を崩す → 再検出で部屋が消える。
            // Break one wall -> the room vanishes on re-detection.
            world.RemoveBlock(new Vector3Int(2, 2, 0), BlockRemoveReason.ManualRemove);
            datastore.RebuildAll();
            Assert.AreEqual(0, datastore.Rooms.Count, "room must vanish");

            // 旧状態は破棄されず Degraded・N=150・猶予作動。
            // Old state survives as a Degraded orphan with N preserved and grace running.
            Assert.True(datastore.TryGetDegradedOrphan(out var orphan));
            Assert.AreEqual(CleanRoomRoomStatus.Degraded, orphan.Status);
            Assert.AreEqual(150.0, orphan.ImpurityCount, 1e-6);
            Assert.Greater(orphan.GraceRemainingSeconds, 0.0);
        }

        [Test]
        public void Datastore_ResealWithinGrace_RecoversValid_CarriesImpurityAndThresholdIndex()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var datastore = serviceProvider.GetService<CleanRoomDatastore>();
            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));
            datastore.RebuildAll();

            datastore.Rooms[0].AddImpurity(150.0);
            datastore.Rooms[0].SetThresholdIndex(2); // 引き継ぎ検証用に行2を仕込む

            world.RemoveBlock(new Vector3Int(2, 2, 0), BlockRemoveReason.ManualRemove);
            datastore.RebuildAll();
            GameUpdater.RunFrames(50); // 猶予100tick未満

            // 同じ位置に壁を戻す → 再検出で部屋復活。
            // Restore the wall -> room reappears on re-detection.
            world.TryAddBlock(ForUnitTestModBlockId.CleanRoomWallId, new Vector3Int(2, 2, 0),
                BlockDirection.North, System.Array.Empty<BlockCreateParam>(), out _);
            datastore.RebuildAll();

            Assert.AreEqual(1, datastore.Rooms.Count);
            var recovered = datastore.Rooms[0];
            Assert.AreEqual(CleanRoomRoomStatus.Valid, recovered.Status);
            Assert.AreEqual(150.0, recovered.ImpurityCount, 1e-6, "N carried across reseal");
            Assert.AreEqual(2, recovered.ThresholdIndex, "threshold row carried across reseal");
            Assert.False(datastore.TryGetDegradedOrphan(out _), "orphan consumed by reseal");
        }

        [Test]
        public void Datastore_GraceExpires_GoesInvalid_ThenDiscardedOnNextRebuild()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var datastore = serviceProvider.GetService<CleanRoomDatastore>();
            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));
            datastore.RebuildAll();

            datastore.Rooms[0].AddImpurity(150.0);
            world.RemoveBlock(new Vector3Int(2, 2, 0), BlockRemoveReason.ManualRemove);
            datastore.RebuildAll();
            GameUpdater.RunFrames(120); // 猶予100tick超

            // 猶予切れ → Invalid（N保持）。
            // Grace expired -> Invalid, N retained.
            Assert.True(datastore.TryGetDegradedOrphan(out var expired));
            Assert.AreEqual(CleanRoomRoomStatus.Invalid, expired.Status);
            Assert.AreEqual(150.0, expired.ImpurityCount, 1e-6);

            // 猶予切れ後に再封 → Invalid 孤立は破棄され、新部屋は N=0 から（汚染の「転生」禁止）。
            // Reseal after expiry: the Invalid orphan is discarded; the new room starts clean.
            world.TryAddBlock(ForUnitTestModBlockId.CleanRoomWallId, new Vector3Int(2, 2, 0),
                BlockDirection.North, System.Array.Empty<BlockCreateParam>(), out _);
            datastore.RebuildAll();
            Assert.AreEqual(1, datastore.Rooms.Count);
            Assert.AreEqual(0.0, datastore.Rooms[0].ImpurityCount, 1e-9, "no impurity resurrection");
            Assert.False(datastore.TryGetDegradedOrphan(out _), "Invalid orphan discarded");
        }

        [Test]
        public void Datastore_UnrelatedRebuild_KeepsHoldBandThresholdIndexAndImpurity()
        {
            // must-fix A-1 の非回帰: 無関係な再検出で保持帯（C=9, 行0）の部屋が降格しないこと。
            // Regression for review A-1: an unrelated rebuild must not demote a hold-band room.
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var datastore = serviceProvider.GetService<CleanRoomDatastore>();
            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4)); // V27
            datastore.RebuildAll();

            // 平衡 C=9 を作る: A_total = nq·C = 5×9 = 45、N = 9×27 = 243。
            // Steady C=9: A_total = nq·C = 45, N = 9·27 = 243.
            var room = datastore.Rooms[0];
            datastore.AddAirFilter(new Vector3Int(2, 2, 2), new AirFilterStub(5.0));
            datastore.SetPollutionPerSecondProvider(_ => 45.0);
            room.AddImpurity(243.0);
            room.SetThresholdIndex(0); // 保持帯（8〜10）に居る行0の部屋

            // 平衡なので tick しても行0のまま（サニティ）。
            // Sanity: stays at row 0 under ticking (hold band).
            GameUpdater.RunFrames(1);
            Assert.AreEqual(0, datastore.Rooms[0].ThresholdIndex);

            // 無関係な場所に壁を1個置いて全再検出を強制。
            // Place an unrelated wall far away and force a full rebuild.
            world.TryAddBlock(ForUnitTestModBlockId.CleanRoomWallId, new Vector3Int(50, 0, 0),
                BlockDirection.North, System.Array.Empty<BlockCreateParam>(), out _);
            datastore.RebuildAll();
            GameUpdater.RunFrames(1);

            // 引き継ぎが無いと行が Out リセット→ Decide(Out, 9, …) は行1 へ恒久降格してしまう。
            // Without carry-over the row resets to Out and re-promotes only to row 1.
            Assert.AreEqual(0, datastore.Rooms[0].ThresholdIndex, "hold-band row survives unrelated rebuild");
            Assert.AreEqual(243.0, datastore.Rooms[0].ImpurityCount, 1.0, "N survives unrelated rebuild");
        }

        // 1セルの壁を置くヘルパ。
        // Helper: place a single wall cell.
        private static void PlaceWall(IWorldBlockDatastore world, Vector3Int pos)
        {
            world.TryAddBlock(ForUnitTestModBlockId.CleanRoomWallId, pos,
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
        }

        // min..max の外殻だけ壁を置き、内部を空洞にするヘルパ。
        // Helper: place walls only on the shell of [min,max], leaving the interior hollow.
        private static void BuildWallShell(IWorldBlockDatastore world, Vector3Int min, Vector3Int max)
        {
            for (var x = min.x; x <= max.x; x++)
            for (var y = min.y; y <= max.y; y++)
            for (var z = min.z; z <= max.z; z++)
            {
                var onShell = x == min.x || x == max.x || y == min.y || y == max.y || z == min.z || z == max.z;
                if (!onShell) continue;
                PlaceWall(world, new Vector3Int(x, y, z));
            }
        }
    }
}
