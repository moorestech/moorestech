using UnityEngine;

namespace Client.Game.InGame.Train.RailGraph
{
    internal static class BezierRailMeshCpu
    {
        #region Internal

        // CPU変形を実行する
        // Deform with CPU
        internal static void Deform(BezierRailMesh mesh)
        {
            // 必須データを準備する
            // Ensure required data
            if (!BezierRailMeshData.EnsureMeshData(mesh)) return;
            if (!BezierRailMeshData.EnsureCurveData(mesh)) return;
            EnsureWorkingBuffers(mesh);

            // 変形対象の範囲を決定する
            // Resolve segment range
            var targetSegmentLength = mesh._segmentLength > 0f ? Mathf.Min(mesh._segmentLength, mesh._curveLength) : mesh._curveLength;
            var startDistance = Mathf.Clamp(mesh._distanceOffset, 0f, mesh._curveLength);
            var endDistance = Mathf.Clamp(startDistance + targetSegmentLength, 0f, mesh._curveLength);
            var usableLength = Mathf.Max(1e-4f, endDistance - startDistance);

            if (mesh._alignedVertices == null || mesh._alignedVertices.Length != mesh._originalVertices.Length) mesh._alignedVertices = new Vector3[mesh._originalVertices.Length];

            // 頂点ごとにベジェ変形を行う
            // Deform each vertex along Bezier
            for (var i = 0; i < mesh._alignedVertices.Length; i++)
            {
                var alignedVertex = mesh._axisRotation * mesh._originalVertices[i];
                mesh._alignedVertices[i] = alignedVertex;
                var normalizedForward = (alignedVertex.z - mesh._forwardMin) / mesh._meshLength;
                var distanceOnCurve = startDistance + normalizedForward * usableLength;

                var t = BezierRailMeshData.DistanceToTime(mesh, distanceOnCurve);
                var curvePos = BezierRailMeshData.EvaluatePosition(mesh, t);
                var curveTangent = BezierRailMeshData.EvaluateTangent(mesh, t);
                var rotation = BuildCurveRotation(curveTangent);

                var offset = rotation * new Vector3(alignedVertex.x, alignedVertex.y, 0f);
                mesh._deformedVertices[i] = curvePos + offset;

                var normal = mesh._axisRotation * mesh._originalNormals[i];
                mesh._deformedNormals[i] = rotation * normal;
            }

            // メッシュへ反映する
            // Apply to mesh
            mesh._deformedMesh.vertices = mesh._deformedVertices;
            mesh._deformedMesh.normals = mesh._deformedNormals;
            mesh._deformedMesh.RecalculateBounds();

            if (mesh._meshCollider != null) mesh._meshCollider.sharedMesh = mesh._deformedMesh;
        }

        // 頂点バッファを準備する
        // Ensure working buffers
        internal static void EnsureWorkingBuffers(BezierRailMesh mesh)
        {
            if (mesh._deformedVertices == null || mesh._deformedVertices.Length != mesh._originalVertices.Length) mesh._deformedVertices = new Vector3[mesh._originalVertices.Length];
            if (mesh._deformedNormals == null || mesh._deformedNormals.Length != mesh._originalNormals.Length) mesh._deformedNormals = new Vector3[mesh._originalNormals.Length];
        }

        // レール姿勢の回転を計算する
        // Compute rail rotation
        private static Quaternion BuildCurveRotation(Vector3 tangent)
        {
            var forward = tangent.sqrMagnitude > 1e-6f ? tangent.normalized : Vector3.forward;
            var horizontal = new Vector3(forward.x, 0f, forward.z);

            // ロールを抑制してレールの姿勢を安定させる
            // Keep rails upright without roll
            if (horizontal.sqrMagnitude < 1e-6f)
            {
                var angle = forward.y >= 0f ? 90f : -90f;
                return Quaternion.AngleAxis(angle, Vector3.right);
            }

            var yawRotation = Quaternion.LookRotation(horizontal.normalized, Vector3.up);
            var invYaw = Quaternion.Inverse(yawRotation);
            var localForward = invYaw * forward;
            var pitchAngle = Mathf.Atan2(localForward.y, Mathf.Max(1e-6f, localForward.z)) * Mathf.Rad2Deg;
            return yawRotation * Quaternion.AngleAxis(pitchAngle, Vector3.right);
        }

        #endregion
    }
}
