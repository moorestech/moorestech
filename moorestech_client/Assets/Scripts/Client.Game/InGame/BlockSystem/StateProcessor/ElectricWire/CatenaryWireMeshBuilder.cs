using System;
using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor.ElectricWire
{
    /// <summary>
    /// 両端点と垂れ量からカテナリー曲線に沿ったワイヤーメッシュを生成する
    /// Build a wire mesh along a catenary curve from both endpoints and a sag amount
    /// </summary>
    public static class CatenaryWireMeshBuilder
    {
        // 曲線の分割数とチューブ断面の半径
        // Curve segment count and tube cross-section radius
        public const int SegmentCount = 16;
        public const float WireRadius = 0.03f;

        // t=0,1で垂れ0に正規化するための端点係数 cosh(1)
        // Endpoint coefficient cosh(1) used to normalize sag to zero at t=0,1
        private static readonly double CoshOne = Math.Cosh(1.0);

        /// <summary>
        /// カテナリー曲線メッシュを生成し、クリック判定用のセグメント情報を出力する
        /// Build the catenary curve mesh and output per-segment info for click detection
        /// </summary>
        public static Mesh Build(Vector3 start, Vector3 end, float sag, List<(Vector3 center, Vector3 up, float length)> outColliderSegments)
        {
            // 曲線上の折れ線頂点を計算する
            // Calculate the polyline vertices along the curve
            var points = CalculateCurvePoints(start, end, sag, SegmentCount);

            // セグメントごとのコライダー配置情報を書き出す
            // Write out collider placement info per segment
            BuildColliderSegments(points, outColliderSegments);

            // 折れ線から四角チューブメッシュを構築する
            // Build the square tube mesh from the polyline
            return BuildTubeMesh(points);
        }

        /// <summary>
        /// カテナリー曲線上の頂点列を計算する（純粋計算）
        /// Calculate the vertex list along the catenary curve (pure computation)
        /// </summary>
        public static Vector3[] CalculateCurvePoints(Vector3 start, Vector3 end, float sag, int segmentCount)
        {
            var points = new Vector3[segmentCount + 1];
            var denominator = (float)(CoshOne - 1.0);

            for (var i = 0; i <= segmentCount; i++)
            {
                // 直線補間位置に、正規化した垂れ量を下方向へ加える
                // Add the normalized sag downward onto the linear interpolation position
                var t = (float)i / segmentCount;
                var linear = Vector3.Lerp(start, end, t);

                var droop = denominator <= 0f
                    ? 0f
                    : sag * (float)(CoshOne - Math.Cosh(2.0 * t - 1.0)) / denominator;

                points[i] = linear + Vector3.down * droop;
            }

            return points;
        }

        // セグメントごとの中心・軸方向・長さを算出する
        // Compute center, axis direction, and length per segment
        private static void BuildColliderSegments(Vector3[] points, List<(Vector3 center, Vector3 up, float length)> outColliderSegments)
        {
            if (outColliderSegments == null) return;
            outColliderSegments.Clear();

            for (var i = 0; i < points.Length - 1; i++)
            {
                var a = points[i];
                var b = points[i + 1];

                var diff = b - a;
                var length = diff.magnitude;
                var axis = length > Mathf.Epsilon ? diff / length : Vector3.up;

                outColliderSegments.Add(((a + b) * 0.5f, axis, length));
            }
        }

        // 折れ線に沿って四角断面のチューブメッシュを構築する
        // Build a square cross-section tube mesh along the polyline
        private static Mesh BuildTubeMesh(Vector3[] points)
        {
            var ringCount = points.Length;
            var vertices = new Vector3[ringCount * 4];

            // 各リングに断面4頂点を配置する
            // Place four cross-section vertices per ring
            for (var ring = 0; ring < ringCount; ring++)
            {
                var tangent = ComputeTangent(points, ring);
                var right = Vector3.Cross(Vector3.up, tangent);
                right = right.sqrMagnitude < 1e-6f ? Vector3.right : right.normalized;
                var up = Vector3.Cross(tangent, right).normalized;

                var baseIndex = ring * 4;
                vertices[baseIndex + 0] = points[ring] + (right + up) * WireRadius;
                vertices[baseIndex + 1] = points[ring] + (right - up) * WireRadius;
                vertices[baseIndex + 2] = points[ring] + (-right - up) * WireRadius;
                vertices[baseIndex + 3] = points[ring] + (-right + up) * WireRadius;
            }

            // 隣接リングを4面のクアッドで接続する
            // Connect adjacent rings with four-sided quads
            var triangles = new List<int>((ringCount - 1) * 24);
            for (var ring = 0; ring < ringCount - 1; ring++)
            {
                var b0 = ring * 4;
                var b1 = (ring + 1) * 4;
                for (var k = 0; k < 4; k++)
                {
                    var k2 = (k + 1) % 4;
                    triangles.Add(b0 + k);
                    triangles.Add(b1 + k);
                    triangles.Add(b0 + k2);
                    triangles.Add(b0 + k2);
                    triangles.Add(b1 + k);
                    triangles.Add(b1 + k2);
                }
            }

            var mesh = new Mesh { name = "ElectricWireMesh" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // リング位置での接線を近傍頂点から求める
        // Estimate the tangent at a ring from neighboring vertices
        private static Vector3 ComputeTangent(Vector3[] points, int ring)
        {
            var previous = points[Mathf.Max(ring - 1, 0)];
            var next = points[Mathf.Min(ring + 1, points.Length - 1)];
            var tangent = next - previous;
            return tangent.sqrMagnitude < 1e-6f ? Vector3.forward : tangent.normalized;
        }
    }
}
