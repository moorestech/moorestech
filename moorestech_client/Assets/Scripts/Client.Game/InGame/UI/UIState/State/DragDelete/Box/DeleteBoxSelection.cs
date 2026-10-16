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
    ///     箱選択の局面と始点セル。終点は確定の瞬間に対象へ解決されるので保持しない（BlueprintCopySelection と同形）
    ///     Box-selection phase and its start cell; the end cell resolves into targets at once, so it is not stored (same shape as BlueprintCopySelection)
    /// </summary>
    public class DeleteBoxSelection
    {
        public DeleteBoxPhase Phase { get; private set; } = DeleteBoxPhase.Idle;
        public Vector3Int StartCell { get; private set; }

        public void SelectStart(Vector3Int cell)
        {
            if (Phase != DeleteBoxPhase.Idle) throw new InvalidOperationException($"SelectStart in {Phase}");
            StartCell = cell;
            Phase = DeleteBoxPhase.SelectingEnd;
        }

        public void Clear()
        {
            Phase = DeleteBoxPhase.Idle;
        }
    }
}
