using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.StateProcessor.ElectricWire;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests
{
    /// <summary>
    /// カテナリー曲線の純粋計算部を検証するテスト
    /// Tests verifying the pure computation of the catenary curve
    /// </summary>
    public class CatenaryWireMeshBuilderTest
    {
        private const float Epsilon = 0.0001f;

        [Test]
        public void CurvePoints_EndpointsMatchStartAndEnd()
        {
            // 端点は始点・終点と一致し、垂れの影響を受けないことを確認する
            // Verify endpoints match start/end and are unaffected by sag
            var start = new Vector3(1f, 5f, 2f);
            var end = new Vector3(9f, 5f, 6f);
            var points = CatenaryWireMeshBuilder.CalculateCurvePoints(start, end, sag: 3f, segmentCount: 16);

            Assert.That(Vector3.Distance(points[0], start), Is.LessThan(Epsilon));
            Assert.That(Vector3.Distance(points[points.Length - 1], end), Is.LessThan(Epsilon));
        }

        [Test]
        public void CurvePoints_MidpointSagsExactlyByGivenAmount()
        {
            // 中央点は直線中点よりちょうどsag分だけ下がることを確認する
            // Verify the midpoint sags below the straight-line midpoint by exactly sag
            var start = new Vector3(0f, 10f, 0f);
            var end = new Vector3(8f, 10f, 0f);
            const float sag = 2f;
            var points = CatenaryWireMeshBuilder.CalculateCurvePoints(start, end, sag, segmentCount: 16);

            // segmentCount=16 なので中央インデックスは8
            // With segmentCount=16 the center index is 8
            var straightMidpoint = Vector3.Lerp(start, end, 0.5f);
            var mid = points[8];

            Assert.That(mid.y, Is.EqualTo(straightMidpoint.y - sag).Within(Epsilon));
            Assert.That(mid.x, Is.EqualTo(straightMidpoint.x).Within(Epsilon));
        }

        [Test]
        public void CurvePoints_MidpointIsTheLowestPoint()
        {
            // 中央が最も低く、両端に向かって単調に上がることを確認する
            // Verify the center is the lowest and rises monotonically toward both ends
            var start = new Vector3(0f, 0f, 0f);
            var end = new Vector3(10f, 0f, 0f);
            var points = CatenaryWireMeshBuilder.CalculateCurvePoints(start, end, sag: 1.5f, segmentCount: 16);

            for (var i = 0; i < 8; i++)
            {
                Assert.That(points[i].y, Is.GreaterThanOrEqualTo(points[i + 1].y - Epsilon));
            }
        }

        [Test]
        public void CurvePoints_ProducesSegmentCountPlusOnePoints()
        {
            // 分割数16に対して頂点数は17であることを確認する
            // Verify the vertex count is 17 for a segment count of 16
            var points = CatenaryWireMeshBuilder.CalculateCurvePoints(Vector3.zero, new Vector3(4f, 0f, 0f), sag: 1f, segmentCount: 16);
            Assert.AreEqual(17, points.Length);
        }

        [Test]
        public void Build_OutputsSixteenColliderSegments()
        {
            // Buildは16セグメント分のコライダー情報を出力することを確認する
            // Verify Build outputs collider info for 16 segments
            var colliderSegments = new List<(Vector3 center, Vector3 up, float length)>();
            var mesh = CatenaryWireMeshBuilder.Build(Vector3.zero, new Vector3(6f, 0f, 0f), sag: 0.6f, colliderSegments);

            Assert.AreEqual(CatenaryWireMeshBuilder.SegmentCount, colliderSegments.Count);
            Assert.IsNotNull(mesh);
            Assert.That(mesh.vertexCount, Is.GreaterThan(0));

            // 各セグメントの長さは正で、軸方向は単位ベクトルであること
            // Each segment length is positive and its axis is a unit vector
            foreach (var segment in colliderSegments)
            {
                Assert.That(segment.length, Is.GreaterThan(0f));
                Assert.That(segment.up.magnitude, Is.EqualTo(1f).Within(0.001f));
            }

            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void Build_CurveIsLongerThanStraightLine()
        {
            // 垂れたワイヤーの総延長は直線距離より長いことを確認する
            // Verify the sagged wire total length exceeds the straight-line distance
            var start = Vector3.zero;
            var end = new Vector3(10f, 0f, 0f);
            var colliderSegments = new List<(Vector3 center, Vector3 up, float length)>();
            var mesh = CatenaryWireMeshBuilder.Build(start, end, sag: 2f, colliderSegments);

            var totalLength = 0f;
            foreach (var segment in colliderSegments) totalLength += segment.length;

            Assert.That(totalLength, Is.GreaterThan(Vector3.Distance(start, end)));
            Object.DestroyImmediate(mesh);
        }
    }
}
