using System;
using Game.Block.Interface.Component;
using UniRx;

namespace Game.Block.Blocks.OrbitalCargo
{
    /// <summary>
    ///     1ブロック内で共有する積荷の選択を読む口。発射台の在庫・積載・processor と拠点の在庫・入口が受け取る
    ///     書く口 SetCurrent は持たない。書けるのは実体 OrbitalCargoSelectionState を受け取る選択部品だけ
    ///     Read port for the cargo selection shared within a block, received by the pad's inventory, loader and processor and the base's inventory and inlet
    ///     It has no SetCurrent; only the selector, which receives the concrete OrbitalCargoSelectionState, can write
    /// </summary>
    public interface IOrbitalCargoSelectionReader
    {
        OrbitalCargoSelection Current { get; }
        IObservable<Unit> OnChanged { get; }
    }
}
