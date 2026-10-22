using System.Collections.Generic;
using Client.UitkPoc.VectorEditor.Geometry;
using Client.UitkPoc.VectorEditor.Model;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.UitkPoc.VectorEditor.View
{
    // 図形の束を1枚のPainter2Dで描く層。描いた縮尺と範囲を覚え、ビュー側は差分の拡大と平行移動だけを要素の変形で当てる
    // Layer drawing a batch of shapes with one Painter2D; remembers draw scale and region, the view applies the rest as a transform
    public class ShapeLayerElement : VisualElement
    {
        private readonly List<VectorShape> _shapes = new();
        private bool _cull;

        public float DrawScale { get; private set; } = 1f;
        public Rect DrawnRegion { get; private set; }
        public int DrawnCount { get; private set; }
        public float LastDrawMilliseconds { get; private set; }

        public ShapeLayerElement()
        {
            pickingMode = PickingMode.Ignore;
            usageHints = UsageHints.DynamicTransform;
            style.position = Position.Absolute;
            style.transformOrigin = new TransformOrigin(0, 0);
            generateVisualContent += Draw;
        }

        // cullがtrueのときは、範囲に掛かる図形だけを描く（拡大時の描き直しで画面外を省く）
        // With cull, only shapes overlapping the region are drawn (skips off-screen work when redrawing zoomed in)
        public void SetContent(IEnumerable<VectorShape> shapes, float drawScale, bool cull, Rect region)
        {
            _shapes.Clear();
            _shapes.AddRange(shapes);
            DrawScale = drawScale;
            _cull = cull;
            DrawnRegion = region;
            MarkDirtyRepaint();
        }

        public void ApplyView(Vector2 translation, float zoom)
        {
            style.translate = new Translate(translation.x, translation.y);
            var relative = zoom / DrawScale;
            style.scale = new StyleScale(new Scale(new Vector3(relative, relative, 1f)));
        }

        private void Draw(MeshGenerationContext context)
        {
            // 計測用: パスを積むまでの時間（三角形分割のジョブ待ちは含まない）
            // Instrumentation: time to submit paths (excludes waiting on tessellation jobs)
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var painter = context.painter2D;
            var drawn = 0;
            foreach (var shape in _shapes)
            {
                if (_cull && !DrawnRegion.Overlaps(shape.WorldBounds())) continue;
                ShapePathBuilder.Draw(painter, shape, DrawScale);
                drawn++;
            }
            DrawnCount = drawn;
            LastDrawMilliseconds = (float)stopwatch.Elapsed.TotalMilliseconds;
        }
    }
}
