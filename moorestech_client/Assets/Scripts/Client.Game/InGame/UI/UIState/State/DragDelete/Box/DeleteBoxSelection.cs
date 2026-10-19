using System;
using Client.Game.InGame.UI.UIState.State.DragDelete.Selection;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State.DragDelete.Box
{
    public enum DeleteBoxPhase
    {
        Idle,
        SelectingEnd,
    }

    /// <summary>
    ///     箱の局面・追加か除外か・始点・最後の有効終点を保持する。カーソルが外れても表示範囲は消えない
    ///     Retain the box phase, its add/remove intent, start and last valid end so losing the cursor surface preserves the displayed range
    /// </summary>
    public class DeleteBoxSelection
    {
        public DeleteBoxPhase Phase { get; private set; } = DeleteBoxPhase.Idle;
        public Vector3Int StartCell { get; private set; }
        public Vector3Int EndCell { get; private set; }
        public DeleteSelectionIntent Intent { get; private set; } = DeleteSelectionIntent.Add;

        public void SetEndCell(Vector3Int cell) => EndCell = cell;

        public void SelectStart(Vector3Int cell, DeleteSelectionIntent intent)
        {
            if (Phase != DeleteBoxPhase.Idle) throw new InvalidOperationException($"SelectStart in {Phase}");
            Intent = intent;
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
