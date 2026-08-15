using Client.Game.InGame.Environment.Terrain.Build.Placement;
using Game.MapGeneration.Pipeline.Config;
using NUnit.Framework;
using Server.Protocol.PacketResponse.MapData;

namespace Client.Tests.UnitTest.Terrain.Placement
{
    /// <summary>
    ///     転送された摂動前の高さへ木の摂動を足し直す経路を検証する。サーバーが摂動を止めた分（R12）を
    ///     クライアントが足し損ねても例外にはならず、地面が木の根元で平らなまま静かに出荷される
    ///     Verifies the path re-adding the tree perturbation onto the transferred pre-tree heights; failing to add back
    ///     what the server stopped applying (R12) throws nothing and merely ships flat ground under every tree
    /// </summary>
    public class TreePerturbationApplierTest
    {
        private const int Resolution = 5;
        private const float TerrainSize = 100f;
        private const float TerrainHeight = 50f;
        private const float FlatHeight = 0.5f;

        // heightModWidth=40 は radiusPixels=1.6 になる。中心から1画素は届き2画素は届かない幅で減衰を観測する
        // heightModWidth=40 yields radiusPixels=1.6: one pixel out is reached and two are not, exposing the falloff
        private const float HeightModAmount = 10f;
        private const float HeightModWidth = 40f;

        private const string TreeGuid = "11111111-1111-1111-1111-111111111111";
        private const string RockGuid = "22222222-2222-2222-2222-222222222222";

        // 中心画素[2,2]の真上。RoundToIntで格子へ落ちる座標を選び、丸め方向の議論を持ち込まない
        // Directly over the center pixel [2, 2]; a coordinate landing exactly on the lattice keeps rounding out of the assertions
        private const float CenterLocalPosition = 50f;

        [Test]
        public void RaisesTheHeightUnderATreeByItsHeightModAmount()
        {
            var postHeights = TreePerturbationApplier.Apply(
                CreateFlatHeights(), CreateConfig(), new[] { CreateMapObject(TreeGuid, CenterLocalPosition) });

            // 中心は減衰1.0なので heightModAmount/terrainHeight がそのまま乗る
            // The falloff is 1.0 at the center, so heightModAmount/terrainHeight lands unattenuated
            Assert.That(postHeights[2, 2], Is.EqualTo(FlatHeight + HeightModAmount / TerrainHeight).Within(1e-5f));
        }

        [Test]
        public void FadesTheModificationOutWithDistanceAndStopsAtTheRadius()
        {
            var postHeights = TreePerturbationApplier.Apply(
                CreateFlatHeights(), CreateConfig(), new[] { CreateMapObject(TreeGuid, CenterLocalPosition) });

            // 隣接画素は持ち上がるが中心には届かない。定数を丸ごと足しているだけの実装をここで落とす
            // The neighbour rises without reaching the center, failing an implementation that merely adds a constant
            Assert.That(postHeights[2, 3], Is.GreaterThan(FlatHeight));
            Assert.That(postHeights[2, 3], Is.LessThan(postHeights[2, 2]));

            // radiusPixels=1.6 の外は一切触らない。半径判定を落とすとタイル全面が持ち上がる
            // Nothing outside radiusPixels=1.6 is touched; dropping the radius test would lift the whole tile
            Assert.That(postHeights[2, 4], Is.EqualTo(FlatHeight).Within(1e-6f));
            Assert.That(postHeights[0, 0], Is.EqualTo(FlatHeight).Within(1e-6f));
        }

        [Test]
        public void LeavesTheInputHeightsUntouched()
        {
            // splatとdetail密度は同じ配列を摂動前として読み続ける。破壊的に書くと見た目だけが二重摂動になる
            // Splat and detail density keep reading the same array as pre-tree; mutating it would double-perturb the visuals alone
            var preHeights = CreateFlatHeights();

            var postHeights = TreePerturbationApplier.Apply(
                preHeights, CreateConfig(), new[] { CreateMapObject(TreeGuid, CenterLocalPosition) });

            Assert.That(preHeights[2, 2], Is.EqualTo(FlatHeight).Within(1e-6f));
            Assert.That(postHeights, Is.Not.SameAs(preHeights));
        }

        [Test]
        public void SkipsMapObjectsThatOwnNoHeightModification()
        {
            // 岩・鉱脈も同じMapObjects配列で届く。木だけを選り分けないと岩の周りの地面が盛り上がる
            // Rocks and veins arrive in the same MapObjects array; without sorting trees out, the ground swells around rocks
            var postHeights = TreePerturbationApplier.Apply(
                CreateFlatHeights(), CreateConfig(), new[] { CreateMapObject(RockGuid, CenterLocalPosition) });

            Assert.That(postHeights[2, 2], Is.EqualTo(FlatHeight).Within(1e-6f));
        }

        [Test]
        public void ReadsThePositionAsTileLocalRatherThanSceneAbsolute()
        {
            // シーン絶対座標のまま渡すと格子外を指して無反応になる。この差はタイル外周でしか現れず気付きにくい
            // A scene-absolute coordinate points off the lattice and does nothing; the difference only shows at tile edges
            var sceneAbsolute = CenterLocalPosition + 2f * TerrainSize;

            var postHeights = TreePerturbationApplier.Apply(
                CreateFlatHeights(), CreateConfig(), new[] { CreateMapObject(TreeGuid, sceneAbsolute) });

            Assert.That(postHeights[2, 2], Is.EqualTo(FlatHeight).Within(1e-6f));
        }

        private static MapObjectLayoutMessagePack CreateMapObject(string mapObjectGuid, float localPosition)
        {
            return new MapObjectLayoutMessagePack(1, mapObjectGuid, localPosition, 0f, localPosition);
        }

        private static float[,] CreateFlatHeights()
        {
            var heights = new float[Resolution, Resolution];
            for (var z = 0; z < Resolution; z++)
            for (var x = 0; x < Resolution; x++)
                heights[z, x] = FlatHeight;

            return heights;
        }

        // 木の摂動はguidマップ経由でしか効かない。草原だけにプロトタイプを持たせ、他バイオームは既定の空のまま置く
        // The perturbation only lands through the guid map, so only grassland owns a prototype and the rest stay empty
        private static TerrainGenerationConfig CreateConfig()
        {
            var config = new TerrainGenerationConfig
            {
                overrideResolution = Resolution,
                terrainWidth = TerrainSize,
                terrainLength = TerrainSize,
                terrainHeight = TerrainHeight,
            };

            config.grassland.treePlacement = new TreePlacementConfig
            {
                prototypes = new[]
                {
                    new TreePrototypeEntry
                    {
                        mapObjectGuids = new[] { TreeGuid },
                        heightModAmount = HeightModAmount,
                        heightModWidth = HeightModWidth,
                    },
                },
            };

            return config;
        }
    }
}
