using Client.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Ground;
using Client.Playtest.Operations;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Playtest
{
    /// <summary>
    ///     プレイテストの平坦足場が地表探査に見えることを検証する（見えないと設置Yが実地形へ落ちる）
    ///     Verifies the playtest flat scaffold is visible to the ground probe; otherwise the placement Y drops to the real terrain
    /// </summary>
    public class PlaytestFlatGroundProbeTest
    {
        // 足場の上面。シナリオが絶対座標で使う設置Yの基準
        // The scaffold's top face, the reference placement Y that scenarios use as an absolute coordinate
        private const float ScaffoldTopHeight = 32f;

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
            var probed = GroundHeightProbe.TryGetFootprintMaxGroundHeight(new Vector3Int(3, 32, 2), BlockDirection.North, Vector3Int.one, out var groundHeight);

            Assert.IsTrue(probed, "the ground probe found no ground above the playtest scaffold");
            Assert.AreEqual(ScaffoldTopHeight, groundHeight, 0.01f, "the ground probe returned the real terrain height instead of the scaffold top");
        }

        [Test]
        public void 平坦足場の上で設置セルは足場上面のグリッドへ揃う()
        {
            var resolved = PlacementGroundCellResolver.TryResolveCellFromGround(new Vector3Int(3, 32, 2), BlockDirection.North, Vector3Int.one, 0, out var resolvedPosition);

            Assert.IsTrue(resolved, "the placement cell could not be resolved from the playtest scaffold");
            Assert.AreEqual(new Vector3Int(3, 32, 2), resolvedPosition, "terrain following moved the placement cell off the playtest scaffold");
        }
    }
}
