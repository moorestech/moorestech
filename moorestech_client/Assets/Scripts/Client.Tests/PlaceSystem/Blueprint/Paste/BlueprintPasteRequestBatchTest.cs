using System;
using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Tests.PlaceSystem.Blueprint.Paste
{
    public class BlueprintPasteRequestBatchTest
    {
        [TestCase(64, 1, 64)]
        [TestCase(65, 2, 1)]
        [TestCase(128, 2, 64)]
        [TestCase(129, 3, 1)]
        public void 列を共有上限で分け全原点の順序と識別を保つTest(int originCount, int requestCount, int lastCount)
        {
            var guid = Guid.Parse("12345678-1234-1234-1234-123456789abc");
            var origins = Enumerable.Range(0, originCount).Select(i => new Vector3Int(-i * 3, i % 4, i * 2)).ToList();
            var sunkPerOrigin = origins.Select((origin, index) => index % 3 == 0
                ? new List<BlockPositionInfo> { new(origin, BlockDirection.North, Vector3Int.one) }
                : new List<BlockPositionInfo>()).ToList();
            sunkPerOrigin[^1].Add(new BlockPositionInfo(origins[^1], BlockDirection.East, new Vector3Int(2, 1, 1)));
            var requests = BlueprintRequest.CreatePasteRequests(guid, 3, origins, sunkPerOrigin).ToList();

            // 境界で原点を落としたり重複させず各要求の回転も保つ
            // Preserve every origin exactly once and retain rotation in each request
            for (var chunk = 0; chunk < requests.Count; chunk++)
            {
                var expected = sunkPerOrigin.Skip(chunk * BlueprintRequest.MaxPasteOrigins)
                    .Take(BlueprintRequest.MaxPasteOrigins).SelectMany(footprints => footprints)
                    .Select(footprint => (footprint.OriginalPos, footprint.BlockDirection, footprint.BlockSize));
                var actual = requests[chunk].TerrainSunkBlockFootprints.Select(footprint => footprint.ToPositionInfo())
                    .Select(footprint => (footprint.OriginalPos, footprint.BlockDirection, footprint.BlockSize));
                CollectionAssert.AreEqual(expected, actual);
            }
            Assert.AreEqual(requestCount, requests.Count);
            Assert.AreEqual(lastCount, requests.Last().Origins.Count);
            Assert.IsTrue(requests.All(request => 0 < request.Origins.Count && request.Origins.Count <= BlueprintRequest.MaxPasteOrigins));
            Assert.IsTrue(requests.All(request => request.BlueprintGuidStr == guid.ToString() && request.RotationStep == 3 && request.Operation == BlueprintOperation.Paste));
            CollectionAssert.AreEqual(origins, requests.SelectMany(request => request.Origins).Select(origin => origin.Vector3Int).ToArray());
        }
    }
}
