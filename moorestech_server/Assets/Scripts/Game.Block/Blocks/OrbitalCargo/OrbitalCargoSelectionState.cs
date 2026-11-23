using System;
using Game.Block.Interface.Component;
using UniRx;

namespace Game.Block.Blocks.OrbitalCargo
{
    // 1ブロック内で選択を共有するセル。実体は選択部品だけが受け取り、ほかの部品には読む口として渡す
    // A per-block cell sharing the selection; only the selector receives the concrete type, other components get the read port
    internal class OrbitalCargoSelectionState : IOrbitalCargoSelectionReader
    {
        public OrbitalCargoSelection Current { get; private set; } = OrbitalCargoSelection.Unselected;
        public IObservable<Unit> OnChanged => _changed;

        private readonly Subject<Unit> _changed = new();

        public void SetCurrent(OrbitalCargoSelection selection)
        {
            Current = selection;
            _changed.OnNext(Unit.Default);
        }
    }
}
