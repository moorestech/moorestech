using System.Collections.Generic;
using System.Linq;
using Client.UitkPoc.Editing;

namespace Client.UitkPoc.VectorEditor.Model
{
    public class AddShapesCommand : IUndoableCommand
    {
        private readonly VectorDocument _document;
        private readonly List<VectorShape> _shapes;

        public AddShapesCommand(VectorDocument document, List<VectorShape> shapes)
        {
            _document = document;
            _shapes = shapes;
        }

        public void Do()
        {
            _document.Shapes.AddRange(_shapes);
            _document.NotifyChanged();
        }

        public void Undo()
        {
            foreach (var shape in _shapes) _document.Shapes.Remove(shape);
            _document.NotifyChanged();
        }
    }

    // 削除。戻すときに元の重なり順へ差し戻せるよう、位置を覚えておく
    // Delete; remembers indices so undo restores the original stacking order
    public class DeleteShapesCommand : IUndoableCommand
    {
        private readonly VectorDocument _document;
        private readonly List<(int index, VectorShape shape)> _removed;

        public DeleteShapesCommand(VectorDocument document, HashSet<int> shapeIds)
        {
            _document = document;
            _removed = document.Shapes.Select((shape, index) => (index, shape)).Where(p => shapeIds.Contains(p.shape.Id)).ToList();
        }

        public void Do()
        {
            foreach (var (_, shape) in _removed) _document.Shapes.Remove(shape);
            _document.NotifyChanged();
        }

        public void Undo()
        {
            foreach (var (index, shape) in _removed) _document.Shapes.Insert(index, shape);
            _document.NotifyChanged();
        }
    }

    // 移動・大きさ変更・回転の前後。ドラッグ中に適用済みなのでRecordで積む
    // Before/after of move, resize or rotate; applied during the drag, so pushed with Record
    public class TransformShapesCommand : IUndoableCommand
    {
        private readonly VectorDocument _document;
        private readonly Dictionary<VectorShape, ShapeTransform> _before;
        private readonly Dictionary<VectorShape, ShapeTransform> _after;

        public TransformShapesCommand(VectorDocument document, Dictionary<VectorShape, ShapeTransform> before, Dictionary<VectorShape, ShapeTransform> after)
        {
            _document = document;
            _before = before;
            _after = after;
        }

        public void Do() => Apply(_after);
        public void Undo() => Apply(_before);

        private void Apply(Dictionary<VectorShape, ShapeTransform> transforms)
        {
            foreach (var (shape, transform) in transforms) shape.SetTransform(transform);
            _document.NotifyChanged();
        }
    }
}
