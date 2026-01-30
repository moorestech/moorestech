using Game.Train.RailCalc;
using UnityEngine;

namespace Client.Game.InGame.Train.RailGraph
{
    internal static class BezierRailMeshData
    {
        #region Internal

        // メッシュ関連情報を準備する
        // Prepare mesh data
        internal static bool EnsureMeshData(BezierRailMesh mesh)
        {
            if (mesh._meshFilter == null && !mesh.TryGetComponent(out mesh._meshFilter)) return false;
            if (mesh._meshRenderer == null) mesh.TryGetComponent(out mesh._meshRenderer);
            if (mesh._meshCollider == null) mesh.TryGetComponent(out mesh._meshCollider);
            if (mesh._sourceMesh == null) mesh._sourceMesh = mesh._meshFilter.sharedMesh;
            if (mesh._sourceMesh == null) return false;

            if (mesh._originalMesh != mesh._sourceMesh)
            {
                mesh._originalMesh = mesh._sourceMesh;
                mesh._originalVertices = mesh._originalMesh.vertices;
                mesh._originalNormals = mesh._originalMesh.normals;
                mesh._deformedVertices = null;
                mesh._deformedNormals = null;
                mesh._meshDataDirty = true;
            }

            if (mesh._useGpuDeform)
            {
                if (mesh._meshFilter.sharedMesh != mesh._sourceMesh) mesh._meshFilter.sharedMesh = mesh._sourceMesh;
            }
            else
            {
                if (mesh._deformedMesh == null)
                {
                    mesh._deformedMesh = Object.Instantiate(mesh._sourceMesh);
                    mesh._deformedMesh.name = $"{mesh._sourceMesh.name}_Bezier";
                }
                if (mesh._meshFilter.sharedMesh != mesh._deformedMesh) mesh._meshFilter.sharedMesh = mesh._deformedMesh;
            }

            if (mesh._meshDataDirty) RefreshMeshMetrics(mesh);

            if (mesh._meshLength <= 1e-4f) return false;
            if (mesh._useGpuDeform) return BezierRailMeshMaterials.EnsureMaterial(mesh);
            return mesh._originalVertices != null && mesh._originalNormals != null;
        }

        // 曲線データを準備する
        // Prepare curve data
        internal static bool EnsureCurveData(BezierRailMesh mesh)
        {
            if (!mesh._curveDataDirty && mesh._arcLengths != null && mesh._curveLength > 1e-4f) return true;

            var samples = GetCurveSamples(mesh);
            mesh._curveLength = BezierUtility.BuildArcLengthTable(mesh._point0, mesh._point1, mesh._point2, mesh._point3, samples, ref mesh._arcLengths);
            mesh._curveSamples = samples;
            mesh._curveDataDirty = false;
            return mesh._curveLength > 1e-4f;
        }

        // メッシュ軸情報を更新する
        // Refresh mesh axis metrics
        internal static void RefreshMeshMetrics(BezierRailMesh mesh)
        {
            mesh._axisRotation = GetAxisRotation(mesh._forwardAxis, mesh._upAxis);
            if (mesh._sourceMesh == null) { mesh._meshLength = 0f; mesh._meshDataDirty = false; return; }

            var vertices = mesh._sourceMesh.vertices;
            if (vertices == null || vertices.Length == 0) { mesh._meshLength = 0f; mesh._meshDataDirty = false; return; }

            var min = float.PositiveInfinity;
            var max = float.NegativeInfinity;

            // 前方軸の範囲を測定する
            // Measure range along forward axis
            for (var i = 0; i < vertices.Length; i++)
            {
                var aligned = mesh._axisRotation * vertices[i];
                var z = aligned.z;
                if (z < min) min = z;
                if (z > max) max = z;
            }

            mesh._forwardMin = min;
            mesh._meshLength = Mathf.Max(1e-4f, max - min);
            mesh._meshDataDirty = false;
        }

        // サンプル数を取得する
        // Get effective curve samples
        internal static int GetCurveSamples(BezierRailMesh mesh)
        {
            var maxSamples = mesh._useGpuDeform ? BezierRailMesh.MaxCurveSamples : BezierRailMesh.CpuMaxCurveSamples;
            return Mathf.Clamp(mesh._curveSamples, 8, maxSamples);
        }

        // 曲線計算ユーティリティを提供する
        // Provide curve calculation helpers
        internal static float DistanceToTime(BezierRailMesh mesh, float distance) => BezierUtility.DistanceToTime(distance, mesh._curveLength, mesh._arcLengths);

        internal static Vector3 EvaluatePosition(BezierRailMesh mesh, float t) => BezierUtility.GetBezierPoint(mesh._point0, mesh._point1, mesh._point2, mesh._point3, t);

        internal static Vector3 EvaluateTangent(BezierRailMesh mesh, float t) => BezierUtility.GetBezierTangent(mesh._point0, mesh._point1, mesh._point2, mesh._point3, t);

        // メッシュ軸の補正回転を計算する
        // Compute axis correction rotation
        internal static Quaternion GetAxisRotation(Vector3 forwardAxis, Vector3 upAxis)
        {
            var forward = forwardAxis.sqrMagnitude > 1e-4f ? forwardAxis.normalized : Vector3.forward;
            var up = upAxis.sqrMagnitude > 1e-4f ? upAxis.normalized : Vector3.up;

            if (Mathf.Abs(Vector3.Dot(forward, up)) > 0.999f)
            {
                up = Vector3.Cross(forward, Vector3.right);
                if (up.sqrMagnitude < 1e-4f) up = Vector3.Cross(forward, Vector3.up);
                up = up.normalized;
            }

            return Quaternion.Inverse(Quaternion.LookRotation(forward, up));
        }

        // モジュール長を推定する
        // Estimate module length
        internal static float CalculateModuleLength(Mesh mesh, Vector3 forwardAxis, Vector3 upAxis)
        {
            if (mesh == null) return 0f;

            var vertices = mesh.vertices;
            if (vertices == null || vertices.Length == 0) return 0f;

            var axisRotation = GetAxisRotation(forwardAxis, upAxis);
            var min = float.PositiveInfinity;
            var max = float.NegativeInfinity;

            foreach (var vertex in vertices)
            {
                var aligned = axisRotation * vertex;
                if (aligned.z < min) min = aligned.z;
                if (aligned.z > max) max = aligned.z;
            }

            return Mathf.Max(1e-4f, max - min);
        }

        // CPU変形メッシュを解放する
        // Release CPU deformed mesh
        internal static void ReleaseDeformedMesh(BezierRailMesh mesh)
        {
            if (mesh._deformedMesh == null) return;
            Object.Destroy(mesh._deformedMesh);
            mesh._deformedMesh = null;
            if (mesh._meshFilter != null && mesh._sourceMesh != null) mesh._meshFilter.sharedMesh = mesh._sourceMesh;
        }

        #endregion
    }
}
