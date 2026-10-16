using System;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State.DragDelete.Box
{
    public enum DeleteBoxPhase
    {
        Idle,
        SelectingEnd,
    }

    /// <summary>
    ///     箱の局面と始点・最後の有効終点を保持する。カーソルが外れても表示範囲は消えない
    ///     Retain the box phase, start and last valid end so losing the cursor surface preserves the displayed range
    /// </summary>
    public class DeleteBoxSelection
    {
        public DeleteBoxPhase Phase { get; private set; } = DeleteBoxPhase.Idle;
        public Vector3Int StartCell { get; private set; }
        public Vector3Int EndCell { get; private set; }

        public void SetEndCell(Vector3Int cell) => EndCell = cell;

        public void SelectStart(Vector3Int cell)
        {
            if (Phase != DeleteBoxPhase.Idle) throw new InvalidOperationException($"SelectStart in {Phase}");
            StartCell = cell;
            EndCell = cell;
            Phase = DeleteBoxPhase.SelectingEnd;
        }

        public void Clear()
        {
            Phase = DeleteBoxPhase.Idle;
        }
    }
}
