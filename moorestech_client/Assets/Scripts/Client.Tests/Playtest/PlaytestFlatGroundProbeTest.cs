using Client.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Ground;
using Client.Playtest.Operations;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Playtest
{
    /// <summary>
    ///     プレイテストの平坦足場が地表探査に見えるか検証する
    ///     Verifies the playtest flat scaffold is visible to the ground probe; otherwise the placement Y drops to the real terrain
    /// </summary>
    public class PlaytestFlatGroundProbeTest
    {
        // 足場の上面。シナリオが設置Yに使う基準
        // The scaffold's top face, the reference placement Y that scenarios use
        private static readonly float ScaffoldTopHeight = PlaytestSetup.GroundCenter.y + PlaytestSetup.GroundSize.y / 2f;

        // 上面より下の入力Y。素通し改変ならこの値が残る
        // An input Y well below the top face, so a pass-through resolver leaves it behind and is caught
        private static readonly Vector3Int ProbeCell = new(3, 5, 2);

        private GameObject _flatGround;

        [SetUp]
        public void CreateScaffold()
        {
            _flatGround = PlaytestSetup.CreateFlatGround();
            Physics.SyncTransforms();
        }

        [TearDown]
        public void DestroyScaffold()
        {
            Object.DestroyImmediate(_flatGround);
        }

        [Test]
        public void 平坦足場は地表探査のレイヤーマスクに引っかかる()
        {
            Assert.AreEqual(LayerConst.GroundLayer, _flatGround.layer, "the playtest scaffold is not on the Ground layer, so the layer-masked ground probe cannot see it");
        }

        [Test]
        public void 平坦足場の上で地表探査は足場上面の高さを返す()
        {
            var probed = GroundHeightProbe.TryGetFootprintMaxGroundHeight(ProbeCell, BlockDirection.North, Vector3Int.one, out var groundHeight);

            Assert.IsTrue(probed, "the ground probe found no ground above the playtest scaffold");
            Assert.AreEqual(ScaffoldTopHeight, groundHeight, 0.01f, "the ground probe returned the real terrain height instead of the scaffold top");
        }

        [Test]
        public void 平坦足場の上で設置セルは足場上面のグリッドへ揃う()
        {
            var resolved = PlacementGroundCellResolver.TryResolveCellFromGround(ProbeCell, BlockDirection.North, Vector3Int.one, 0, out var resolvedPosition);

            Assert.IsTrue(resolved, "the placement cell could not be resolved from the playtest scaffold");
            Assert.AreEqual(new Vector3Int(ProbeCell.x, Mathf.RoundToInt(ScaffoldTopHeight), ProbeCell.z), resolvedPosition, "the placement cell did not land on the playtest scaffold's top face");
        }
    }
}
