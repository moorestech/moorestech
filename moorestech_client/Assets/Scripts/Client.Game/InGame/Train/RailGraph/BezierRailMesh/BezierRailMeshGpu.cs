using UnityEngine;

namespace Client.Game.InGame.Train.RailGraph
{
    internal static class BezierRailMeshGpu
    {
        #region Internal

        // GPU変形を実行する
        // Deform with GPU
        internal static void Deform(BezierRailMesh mesh)
        {
            // 必須データを準備する
            // Ensure required data
            if (!BezierRailMeshData.EnsureMeshData(mesh)) return;
            if (!BezierRailMeshData.EnsureCurveData(mesh)) return;

            // シェーダーパラメータを適用する
            // Apply shader parameters
            ApplyPropertyBlock(mesh);
        }

        // シェーダーパラメータを適用する
        // Apply shader parameters
        internal static void ApplyPropertyBlock(BezierRailMesh mesh)
        {
            if (mesh._meshRenderer == null && !mesh.TryGetComponent(out mesh._meshRenderer)) return;
            if (mesh._propertyBlock == null) mesh._propertyBlock = new MaterialPropertyBlock();
            mesh._propertyBlock.Clear();

            var targetSegmentLength = mesh._segmentLength > 0f ? Mathf.Min(mesh._segmentLength, mesh._curveLength) : mesh._curveLength;
            var startDistance = Mathf.Clamp(mesh._distanceOffset, 0f, mesh._curveLength);
            var endDistance = Mathf.Clamp(startDistance + targetSegmentLength, 0f, mesh._curveLength);
            var usableLength = Mathf.Max(1e-4f, endDistance - startDistance);

            UpdateArcLengthBuffer(mesh);

            // 制御点をローカル空間に変換する
            // Convert control points into local space
            var localP0 = mesh.transform.InverseTransformPoint(mesh._point0);
            var localP1 = mesh.transform.InverseTransformPoint(mesh._point1);
            var localP2 = mesh.transform.InverseTransformPoint(mesh._point2);
            var localP3 = mesh.transform.InverseTransformPoint(mesh._point3);

            mesh._propertyBlock.SetVector(BezierRailMesh.DeformP0Id, localP0);
            mesh._propertyBlock.SetVector(BezierRailMesh.DeformP1Id, localP1);
            mesh._propertyBlock.SetVector(BezierRailMesh.DeformP2Id, localP2);
            mesh._propertyBlock.SetVector(BezierRailMesh.DeformP3Id, localP3);
            mesh._propertyBlock.SetVector(BezierRailMesh.DeformAxisRotationId, new Vector4(mesh._axisRotation.x, mesh._axisRotation.y, mesh._axisRotation.z, mesh._axisRotation.w));
            mesh._propertyBlock.SetFloat(BezierRailMesh.DeformCurveLengthId, mesh._curveLength);
            mesh._propertyBlock.SetFloat(BezierRailMesh.DeformSegmentStartId, startDistance);
            mesh._propertyBlock.SetFloat(BezierRailMesh.DeformSegmentLengthId, usableLength);
            mesh._propertyBlock.SetFloat(BezierRailMesh.DeformForwardMinId, mesh._forwardMin);
            mesh._propertyBlock.SetFloat(BezierRailMesh.DeformMeshLengthId, mesh._meshLength);
            mesh._propertyBlock.SetFloat(BezierRailMesh.DeformSampleCountId, mesh._curveSamples);
            mesh._propertyBlock.SetFloatArray(BezierRailMesh.DeformArcLengthsId, mesh._arcLengthBuffer);
            mesh._propertyBlock.SetColor(BezierRailMesh.PreviewColorId, mesh._previewColor);

            mesh._meshRenderer.SetPropertyBlock(mesh._propertyBlock);
            UpdateRendererBounds(mesh, startDistance, endDistance);
        }

        // アーク長テーブルを固定長配列に転写する
        // Copy arc-length table into fixed buffer
        internal static void UpdateArcLengthBuffer(BezierRailMesh mesh)
        {
            if (mesh._arcLengthBuffer == null || mesh._arcLengthBuffer.Length != BezierRailMesh.MaxCurveSamples + 1) mesh._arcLengthBuffer = new float[BezierRailMesh.MaxCurveSamples + 1];

            var source = mesh._arcLengths;
            var copyLength = source == null ? 0 : Mathf.Min(source.Length, mesh._arcLengthBuffer.Length);

            // 不足分は0で埋める
            // Fill missing slots with zeros
            for (var i = 0; i < copyLength; i++) mesh._arcLengthBuffer[i] = source[i];
            for (var i = copyLength; i < mesh._arcLengthBuffer.Length; i++) mesh._arcLengthBuffer[i] = 0f;
        }

        // GPU変形時の描画範囲を更新する
        // Update renderer bounds for GPU deformation
        internal static void UpdateRendererBounds(BezierRailMesh mesh, float startDistance, float endDistance)
        {
            if (!mesh._useGpuDeform) return;
            if (mesh._meshRenderer == null && !mesh.TryGetComponent(out mesh._meshRenderer)) return;
            if (mesh._sourceMesh == null) return;

            var tStart = BezierRailMeshData.DistanceToTime(mesh, startDistance);
            var tEnd = BezierRailMeshData.DistanceToTime(mesh, endDistance);
            var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            var padding = Mathf.Max(mesh._sourceMesh.bounds.extents.x, Mathf.Max(mesh._sourceMesh.bounds.extents.y, mesh._sourceMesh.bounds.extents.z));
            var step = 1f / BezierRailMesh.BoundsSampleCount;

            // 曲線上の点をサンプリングしてAABBを作る
            // Sample curve points to build AABB
            for (var i = 0; i <= BezierRailMesh.BoundsSampleCount; i++)
            {
                var t = Mathf.Lerp(tStart, tEnd, i * step);
                var worldPoint = BezierRailMeshData.EvaluatePosition(mesh, t);
                var localPoint = mesh.transform.InverseTransformPoint(worldPoint);
                min = Vector3.Min(min, localPoint);
                max = Vector3.Max(max, localPoint);
            }

            var bounds = new Bounds();
            bounds.SetMinMax(min - Vector3.one * padding, max + Vector3.one * padding);
            mesh._meshRenderer.localBounds = bounds;
        }

        #endregion
    }
}
