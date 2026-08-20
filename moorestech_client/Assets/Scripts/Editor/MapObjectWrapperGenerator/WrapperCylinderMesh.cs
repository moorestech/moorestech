using System;
using UnityEngine;

// レイターゲットの形に使うUnity組み込み円柱メッシュと、その多角形近似を織り込んだ半径換算
// The builtin cylinder mesh used as the ray target shape, plus the radius conversion that accounts for its polygonal approximation
public static class WrapperCylinderMesh
{
    // 前例Tree.prefabのレイターゲットが参照するプリミティブ円柱(unity default resourcesのfileID 10206)を引く名前
    // The name that resolves to the primitive cylinder the precedent Tree.prefab's ray target uses (fileID 10206 of unity default resources)
    private const string ResourceName = "New-Cylinder.fbx";

    // 蓋の三角形と縮退三角形を落とす閾値。側面の法線は桁違いに大きい
    // Threshold that drops cap and degenerate triangles; a side face normal is orders of magnitude larger
    private const float SideFaceNormalThreshold = 1e-12f;

    public static Mesh Resolve()
    {
        var mesh = Resources.GetBuiltinResource<Mesh>(ResourceName);
        if (mesh == null) throw new InvalidOperationException($"builtin cylinder mesh not found: {ResourceName}");
        return mesh;
    }

    // 側面は多角形なので、外接円半径でスケールすると面の中央方位が要求半径に届かず、そこだけBK自前コライダーがはみ出して先にレイを取る
    // The side wall is a polygon, so scaling by its circumradius leaves the face midpoints short of the required radius and BK's own colliders poke out there to take the ray first
    public static float CalculateInscribedRadius(Mesh mesh)
    {
        var center = new Vector2(mesh.bounds.center.x, mesh.bounds.center.z);
        var vertices = mesh.vertices;
        var triangles = mesh.triangles;
        var inscribedRadius = float.PositiveInfinity;

        for (var index = 0; index < triangles.Length; index += 3)
        {
            var first = vertices[triangles[index]];
            var normal = Vector3.Cross(vertices[triangles[index + 1]] - first, vertices[triangles[index + 2]] - first);

            // 半径を決めるのは側面だけで、上下の蓋は水平方向の法線成分を持たない
            // Only the side faces define the radius; the top and bottom caps carry no horizontal normal component
            var horizontalNormal = new Vector2(normal.x, normal.z);
            if (horizontalNormal.sqrMagnitude < SideFaceNormalThreshold) continue;
            inscribedRadius = Mathf.Min(inscribedRadius, Mathf.Abs(Vector2.Dot(horizontalNormal.normalized, new Vector2(first.x, first.z) - center)));
        }

        if (float.IsInfinity(inscribedRadius) || inscribedRadius <= 0f) throw new InvalidOperationException($"cylinder mesh has no side face: {mesh.name}");
        return inscribedRadius;
    }
}
