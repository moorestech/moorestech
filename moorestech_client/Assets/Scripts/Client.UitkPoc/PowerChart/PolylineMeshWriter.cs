using UnityEngine;
using UnityEngine.UIElements;

namespace Client.UitkPoc.PowerChart
{
    // Painter2Dを通さず、線分ごとの四角形を直接メッシュへ書く軽量な折れ線
    // Lightweight polyline writing one quad per segment straight into the mesh, bypassing Painter2D
    public static class PolylineMeshWriter
    {
        public static void Write(MeshGenerationContext context, Vector2[] points, int count, float width, Color color)
        {
            if (count < 2) return;
            var segments = count - 1;
            var mesh = context.Allocate(segments * 4, segments * 6);
            var half = width / 2f;
            var tint = (Color32)color;
            for (var i = 0; i < segments; i++)
            {
                // 線分の法線方向へ半幅ずらした4頂点
                // Four vertices offset by half width along the segment normal
                var from = points[i];
                var to = points[i + 1];
                var direction = (to - from).normalized;
                var normal = new Vector2(-direction.y, direction.x) * half;
                mesh.SetNextVertex(new Vertex { position = new Vector3(from.x + normal.x, from.y + normal.y, Vertex.nearZ), tint = tint });
                mesh.SetNextVertex(new Vertex { position = new Vector3(to.x + normal.x, to.y + normal.y, Vertex.nearZ), tint = tint });
                mesh.SetNextVertex(new Vertex { position = new Vector3(to.x - normal.x, to.y - normal.y, Vertex.nearZ), tint = tint });
                mesh.SetNextVertex(new Vertex { position = new Vector3(from.x - normal.x, from.y - normal.y, Vertex.nearZ), tint = tint });
                var baseIndex = (ushort)(i * 4);
                mesh.SetNextIndex(baseIndex);
                mesh.SetNextIndex((ushort)(baseIndex + 1));
                mesh.SetNextIndex((ushort)(baseIndex + 2));
                mesh.SetNextIndex(baseIndex);
                mesh.SetNextIndex((ushort)(baseIndex + 2));
                mesh.SetNextIndex((ushort)(baseIndex + 3));
            }
        }
    }
}
