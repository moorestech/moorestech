using System.Collections.Generic;
using Game.MapGeneration.Export;
using Game.MapGeneration.Pipeline;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration
{
    // MapInfoJsonBuilder が instanceId 連番採番と mapVeins 転記を正しく行うことを検証する。
    // Verify MapInfoJsonBuilder assigns sequential instanceIds and transcribes mapVeins correctly.
    public class MapInfoJsonBuilderTest
    {
        // 4成分すべてが違う姿勢。成分を取り違える転記が通らない値にしてある
        // A rotation with four distinct components, chosen so a transcription that swaps them cannot pass
        private static readonly Quaternion DummyRotation = new(0.0381346f, 0.1893079f, 0.2392983f, 0.9515485f);

        [Test]
        public void MapObjectsGetSequentialInstanceIds()
        {
            var output = CreateDummyOutput();

            var mapInfoJson = MapInfoJsonBuilder.Build(output);

            Assert.That(mapInfoJson.MapObjects.Count, Is.EqualTo(3));
            for (var i = 0; i < mapInfoJson.MapObjects.Count; i++)
                Assert.That(mapInfoJson.MapObjects[i].InstanceId, Is.EqualTo(i));
        }

        [Test]
        public void VeinsAreTranscribedVerbatim()
        {
            var output = CreateDummyOutput();

            var mapInfoJson = MapInfoJsonBuilder.Build(output);

            Assert.That(mapInfoJson.MapVeins.Count, Is.EqualTo(2));

            var first = mapInfoJson.MapVeins[0];
            Assert.That(first.VeinGuidStr, Is.EqualTo("11111111-1111-1111-1111-111111111111"));
            Assert.That(first.MinX, Is.EqualTo(1));
            Assert.That(first.MinY, Is.EqualTo(2));
            Assert.That(first.MinZ, Is.EqualTo(3));
            Assert.That(first.MaxX, Is.EqualTo(4));
            Assert.That(first.MaxY, Is.EqualTo(5));
            Assert.That(first.MaxZ, Is.EqualTo(6));

            var second = mapInfoJson.MapVeins[1];
            Assert.That(second.VeinGuidStr, Is.EqualTo("22222222-2222-2222-2222-222222222222"));
        }

        [Test]
        public void ItemAndFluidVeinsMergeIntoMapVeinsInOrder()
        {
            var output = CreateDummyOutput();
            output.FluidVeins = new List<PlacedVein>
            {
                new()
                {
                    VeinGuid = "33333333-3333-3333-3333-333333333333",
                    Min = new Vector3Int(13, 14, 15),
                    Max = new Vector3Int(16, 17, 18),
                },
            };

            var mapInfoJson = MapInfoJsonBuilder.Build(output);

            // ItemVeins(2件)の後にFluidVeins(1件)が続く合流順であること。
            // ItemVeins (2) must be followed by FluidVeins (1) in the merged output.
            Assert.That(mapInfoJson.MapVeins.Count, Is.EqualTo(3));
            Assert.That(mapInfoJson.MapVeins[0].VeinGuidStr, Is.EqualTo("11111111-1111-1111-1111-111111111111"));
            Assert.That(mapInfoJson.MapVeins[1].VeinGuidStr, Is.EqualTo("22222222-2222-2222-2222-222222222222"));

            var fluid = mapInfoJson.MapVeins[2];
            Assert.That(fluid.VeinGuidStr, Is.EqualTo("33333333-3333-3333-3333-333333333333"));
            Assert.That(fluid.MinX, Is.EqualTo(13));
            Assert.That(fluid.MinY, Is.EqualTo(14));
            Assert.That(fluid.MinZ, Is.EqualTo(15));
            Assert.That(fluid.MaxX, Is.EqualTo(16));
            Assert.That(fluid.MaxY, Is.EqualTo(17));
            Assert.That(fluid.MaxZ, Is.EqualTo(18));
        }

        // 姿勢は配置器が斜面法線とランダムYから計算する。map.jsonが落とすと全個体が同じ向きで直立する。
        // The placers derive the rotation from the slope normal and a random yaw; dropping it in map.json stands every instance up alike.
        [Test]
        public void RotationIsTranscribed()
        {
            var mapInfoJson = MapInfoJsonBuilder.Build(CreateDummyOutput());

            Assert.That(mapInfoJson.MapObjects[0].Rotation, Is.EqualTo(DummyRotation));
            Assert.That(mapInfoJson.MapObjects[1].Rotation, Is.EqualTo(Quaternion.identity));
        }

        // スケールは岩周辺テクスチャの入力。map.jsonが落とすと再起動後だけ見た目が変わる。
        // Scale feeds the rock surround texture; dropping it in map.json changes the visuals only after a restart.
        [Test]
        public void ScaleIsTranscribed()
        {
            var output = CreateDummyOutput();

            var mapInfoJson = MapInfoJsonBuilder.Build(output);

            var first = mapInfoJson.MapObjects[0];
            Assert.That(first.Scale, Is.EqualTo(new Vector3(1.5f, 2f, 2.5f)));
        }

        [Test]
        public void SpawnPointIsTranscribed()
        {
            var output = CreateDummyOutput();

            var mapInfoJson = MapInfoJsonBuilder.Build(output);

            Assert.That(mapInfoJson.DefaultSpawnPointJson.Position, Is.EqualTo(output.SpawnPoint));
        }

        private static MapGenerationOutput CreateDummyOutput()
        {
            var output = new MapGenerationOutput
            {
                Resolution = 2,
                SpawnPoint = new Vector3(10, 20, 30),
                MapObjects = new List<PlacedMapObject>
                {
                    new()
                    {
                        MapObjectGuid = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                        Position = new Vector3(1, 1, 1),
                        Rotation = DummyRotation,
                        Scale = new Vector3(1.5f, 2f, 2.5f),
                    },
                    new()
                    {
                        MapObjectGuid = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
                        Position = new Vector3(2, 2, 2),
                        Rotation = Quaternion.identity,
                        Scale = Vector3.one,
                    },
                    new()
                    {
                        MapObjectGuid = "cccccccc-cccc-cccc-cccc-cccccccccccc",
                        Position = new Vector3(3, 3, 3),
                        Rotation = Quaternion.identity,
                        Scale = Vector3.one,
                    },
                },
                ItemVeins = new List<PlacedVein>
                {
                    new()
                    {
                        VeinGuid = "11111111-1111-1111-1111-111111111111",
                        Min = new Vector3Int(1, 2, 3),
                        Max = new Vector3Int(4, 5, 6),
                    },
                    new()
                    {
                        VeinGuid = "22222222-2222-2222-2222-222222222222",
                        Min = new Vector3Int(7, 8, 9),
                        Max = new Vector3Int(10, 11, 12),
                    },
                },
            };
            output.Tiles.Add(new TerrainTileOutput { TileX = 0, TileZ = 0, Heights = new float[4] });
            return output;
        }
    }
}
