using System;
using System.Collections.Generic;
using Client.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.Run;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Ground;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PlaceSystem.Ground
{
    // 列生成と地形追従の合成を実コライダーで検証する（設置システム本体が組む手順そのもの）
    // Verify the composition of run building and terrain following against real colliders, exactly as the placement system composes it
    public class PlacementGroundFollowStepTest
    {
        private readonly List<GameObject> _slabs = new();
        private readonly PlacementGroundFollowStep _followStep = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var slab in _slabs) UnityEngine.Object.DestroyImmediate(slab);
            _slabs.Clear();
        }

        // 地面から始めた横方向のドラッグは各セルがそれぞれの地形へ階段状に追従する
        // A horizontal drag started on the ground makes each cell follow its own terrain like a staircase
        [Test]
        public void 地面ヒットの横方向列は各セルが地形へ追従する()
        {
            CreateCellSlab(1000, 1100, 9.9f);
            CreateCellSlab(1001, 1100, 13.9f);
            CreateCellSlab(1002, 1100, 17.9f);

            var run = BuildRun(new Vector3Int(1000, 0, 1100), new Vector3Int(1002, 0, 1100));

            _followStep.FollowGround(run, PlacementHitSurfaceKind.Ground, Vector3Int.one, 0);

            // 最高点→Y: 11/15/19
            // Maxima → Y: 11/15/19
            Assert.AreEqual(11, run.Cells[0].Position.y);
            Assert.AreEqual(15, run.Cells[1].Position.y);
            Assert.AreEqual(19, run.Cells[2].Position.y);
        }

        // ブロック面から始めた列は整数グリッドのまま触らない
        // A run started on a block face stays on the integer grid
        [Test]
        public void ブロック面ヒットの列は追従しない()
        {
            CreateCellSlab(1200, 1300, 9.9f);

            var run = BuildRun(new Vector3Int(1200, 4, 1300), new Vector3Int(1200, 4, 1300));

            _followStep.FollowGround(run, PlacementHitSurfaceKind.BlockFace, Vector3Int.one, 0);

            Assert.AreEqual(4, run.Cells[0].Position.y);
        }

        // Y軸へ伸びた列を追従させると全セルが1セルへ潰れる
        // Following a Y-axis run would collapse every cell into one
        [Test]
        public void 縦積み列は追従しない()
        {
            CreateCellSlab(1400, 1500, 9.9f);

            var run = BuildRun(new Vector3Int(1400, 20, 1500), new Vector3Int(1400, 22, 1500));
            Assert.AreEqual(PlacementRunAxis.Y, run.Axis);

            _followStep.FollowGround(run, PlacementHitSurfaceKind.Ground, Vector3Int.one, 0);

            Assert.AreEqual(20, run.Cells[0].Position.y);
            Assert.AreEqual(21, run.Cells[1].Position.y);
            Assert.AreEqual(22, run.Cells[2].Position.y);
        }

        // 地表の無いセルだけが不可になり、他のセルは解決される
        // Only the cell without ground is blocked; the others still resolve
        [Test]
        public void 地表の無いセルは設置不可になり他セルは解決される()
        {
            CreateCellSlab(1600, 1700, 5.9f);

            var run = BuildRun(new Vector3Int(1600, 0, 1700), new Vector3Int(1601, 0, 1700));

            _followStep.FollowGround(run, PlacementHitSurfaceKind.Ground, Vector3Int.one, 0);

            Assert.AreEqual(7, run.Cells[0].Position.y);
            Assert.IsTrue(run.Cells[0].Placeable);
            Assert.AreEqual(PlacementBlockCause.None, run.BlockCauses[0]);

            Assert.IsFalse(run.Cells[1].Placeable);
            Assert.AreEqual(PlacementBlockCause.GroundNotFound, run.BlockCauses[1]);
        }

        private static PlacementRun BuildRun(Vector3Int startCell, Vector3Int endCell)
        {
            return CommonBlockPlacePointCalculator.CalculateRun(startCell, endCell, BlockDirection.North, MakeMockBlock());
        }

        // セル1つ分をちょうど覆う段。隣接セルへはみ出さないので階段の各段を独立に作れる
        // A slab covering exactly one cell; it never spills into the neighbour, so each step stands alone
        private void CreateCellSlab(int cellX, int cellZ, float centerY)
        {
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.layer = LayerConst.GroundLayer;
            slab.transform.position = new Vector3(cellX + 0.5f, centerY, cellZ + 0.5f);
            slab.transform.localScale = Vector3.one;
            Physics.SyncTransforms();
            _slabs.Add(slab);
        }

        // ゼロGuidのモック要素はMasterHolderを引かないため実DI起動が要らない
        // A zero-Guid mock element never reads MasterHolder, so it needs no real DI boot
        private static BlockMasterElement MakeMockBlock()
        {
            return new BlockMasterElement(
                0,
                Guid.Empty,
                "TestBlock",
                "TestBlockType",
                null,
                1, // placementsPerCost
                null,
                "テスト",
                "テスト",
                0,
                false,
                Vector3Int.one,
                null,
                null,
                null
            );
        }
    }
}
