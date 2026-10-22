using System;
using System.Collections.Generic;
using System.Linq;
using Client.UitkPoc.Editing;
using Client.UitkPoc.VectorEditor.Model;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.UitkPoc.VectorEditor.View
{
    public enum VectorTool
    {
        Select,
        Rectangle,
        Ellipse,
        Pen,
    }

    // Figma風キャンバス。静止層（全図形）と操作層（ドラッグ中の図形）に分け、ズーム後は今の縮尺で静止層を描き直す
    // Figma-like canvas split into a static layer (all shapes) and an active layer (dragged shapes); redraws statics at the new zoom
    public class VectorCanvas : VisualElement
    {
        public const float MinZoom = 0.05f;
        public const float MaxZoom = 64f;
        private const long RedrawDelayMilliseconds = 150;

        public readonly VectorDocument Document;
        public readonly UndoStack History = new();
        public readonly HashSet<VectorShape> Selected = new();
        public readonly ShapeLayerElement StaticLayer = new();
        public readonly ShapeLayerElement ActiveLayer = new();
        public readonly SelectionOverlayElement Overlay;
        private readonly HashSet<VectorShape> _activeShapes = new();
        private readonly Subject<VectorTool> _toolChanged = new();
        private IVisualElementScheduledItem _pendingRedraw;

        public Vector2 Translation { get; private set; }
        public float Zoom { get; private set; } = 1f;
        public bool RedrawOnZoom { get; private set; } = true;
        public VectorTool Tool { get; private set; } = VectorTool.Select;
        public IObservable<VectorTool> ToolChanged => _toolChanged;

        public VectorCanvas(VectorDocument document)
        {
            Document = document;
            focusable = true;
            AddToClassList("ve-canvas");
            Overlay = new SelectionOverlayElement(this);
            Add(StaticLayer);
            Add(ActiveLayer);
            Add(Overlay);
            document.Changed.Subscribe(_ => OnDocumentChanged());
            RegisterCallback<GeometryChangedEvent>(_ => RedrawStatic());
            new VectorInputController(this);
        }

        public void SetTool(VectorTool tool)
        {
            Tool = tool;
            _toolChanged.OnNext(tool);
            Overlay.MarkDirtyRepaint();
        }

        public void SetRedrawOnZoom(bool redraw)
        {
            RedrawOnZoom = redraw;
            RedrawStatic();
        }

        public Vector2 ToWorld(Vector2 local) => (local - Translation) / Zoom;
        public Vector2 ToLocal(Vector2 world) => world * Zoom + Translation;
        public Rect ViewportWorld => new(ToWorld(Vector2.zero), contentRect.size / Zoom);

        public void SetView(Vector2 translation, float zoom)
        {
            var zoomChanged = !Mathf.Approximately(zoom, Zoom);
            Translation = translation;
            Zoom = Mathf.Clamp(zoom, MinZoom, MaxZoom);
            StaticLayer.ApplyView(Translation, Zoom);
            ActiveLayer.ApplyView(Translation, Zoom);
            Overlay.MarkDirtyRepaint();

            // 描き直す方式では、ズームが止まったら、またはパンで描いた範囲を外れたら静止層を描き直す
            // In redraw mode, redraw statics once zooming settles or panning leaves the drawn region
            if (!RedrawOnZoom) return;
            if (zoomChanged || !Contains(StaticLayer.DrawnRegion, ViewportWorld)) ScheduleStaticRedraw();
        }

        public void ZoomAt(Vector2 local, float zoom)
        {
            var anchor = ToWorld(local);
            var clamped = Mathf.Clamp(zoom, MinZoom, MaxZoom);
            SetView(local - anchor * clamped, clamped);
        }

        // ドラッグの間だけ、動かす図形を静止層から外して操作層で毎フレーム描く
        // During a drag, moving shapes leave the static layer and are drawn every frame on the active layer
        public void BeginInteraction(IEnumerable<VectorShape> shapes)
        {
            _activeShapes.Clear();
            _activeShapes.UnionWith(shapes);
            RedrawStatic();
            RefreshActive();
        }

        public void RefreshActive()
        {
            ActiveLayer.SetContent(_activeShapes, Zoom, false, Rect.zero);
            ActiveLayer.ApplyView(Translation, Zoom);
            Overlay.MarkDirtyRepaint();
        }

        public void EndInteraction()
        {
            _activeShapes.Clear();
            RefreshActive();
            RedrawStatic();
        }

        public void SetSelection(IEnumerable<VectorShape> shapes)
        {
            Selected.Clear();
            foreach (var shape in shapes) Selected.Add(shape);
            Overlay.MarkDirtyRepaint();
        }

        public void RedrawStatic()
        {
            _pendingRedraw?.Pause();
            var visible = Document.Shapes.Where(s => !_activeShapes.Contains(s));
            if (RedrawOnZoom)
            {
                // 表示範囲の上下左右に1画面分の余白を足した範囲だけ、今の縮尺で描く
                // Draw at the current zoom only within the viewport padded by one screen on each side
                var view = ViewportWorld;
                var region = new Rect(view.x - view.width, view.y - view.height, view.width * 3f, view.height * 3f);
                StaticLayer.SetContent(visible, Zoom, true, region);
            }
            else StaticLayer.SetContent(visible, 1f, false, Rect.zero);
            StaticLayer.ApplyView(Translation, Zoom);
            Overlay.MarkDirtyRepaint();
        }

        private void OnDocumentChanged()
        {
            var alive = new HashSet<VectorShape>(Document.Shapes);
            Selected.RemoveWhere(s => !alive.Contains(s));
            RedrawStatic();
        }

        private void ScheduleStaticRedraw()
        {
            _pendingRedraw?.Pause();
            _pendingRedraw = schedule.Execute(RedrawStatic);
            _pendingRedraw.ExecuteLater(RedrawDelayMilliseconds);
        }

        private static bool Contains(Rect outer, Rect inner)
        {
            return outer.xMin <= inner.xMin && outer.yMin <= inner.yMin && outer.xMax >= inner.xMax && outer.yMax >= inner.yMax;
        }
    }
}
