using System;
using System.Collections.Generic;
using UniRx;

namespace Client.UitkPoc.VectorEditor.Model
{
    // 図形の並び（後ろほど手前）。変更はコマンド経由で行い、Changedで描き直しを促す
    // Ordered shapes (later is on top); edits go through commands and Changed prompts a redraw
    public class VectorDocument
    {
        public readonly List<VectorShape> Shapes = new();
        private readonly Subject<Unit> _changed = new();
        private int _nextId = 1;

        public IObservable<Unit> Changed => _changed;

        public int AllocateId()
        {
            return _nextId++;
        }

        public void NotifyChanged()
        {
            _changed.OnNext(Unit.Default);
        }

        public VectorShape Find(int id)
        {
            foreach (var shape in Shapes) if (shape.Id == id) return shape;
            return null;
        }
    }
}
