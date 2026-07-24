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
        public void SpawnPointIsTranscribed()
        {
            var output = CreateDummyOutput();

            var mapInfoJson = MapInfoJsonBuilder.Build(output);

            Assert.That(mapInfoJson.DefaultSpawnPointJson.Position, Is.EqualTo(output.SpawnPoint));
        }

        #region Internal

        private static MapGenerationOutput CreateDummyOutput()
        {
            return new MapGenerationOutput
            {
                Heights = new float[4],
                BiomeIndices = new byte[4],
                Resolution = 2,
                SpawnPoint = new Vector3(10, 20, 30),
                MapObjects = new List<PlacedMapObject>
                {
                    new() { MapObjectGuid = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", Position = new Vector3(1, 1, 1) },
                    new() { MapObjectGuid = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", Position = new Vector3(2, 2, 2) },
                    new() { MapObjectGuid = "cccccccc-cccc-cccc-cccc-cccccccccccc", Position = new Vector3(3, 3, 3) },
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
        }

        #endregion
    }
}
