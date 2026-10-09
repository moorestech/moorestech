using System;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Copy
{
    public enum BlueprintCopyPhase
    {
        SelectingStart,
        SelectingEnd,
        AwaitingName,
        Creating,
    }

    /// <summary>
    ///     コピー範囲の局面と両端セルを一緒に管理する
    ///     Holds the copy phase and its two endpoint cells together
    /// </summary>
    public class BlueprintCopySelection
    {
        public BlueprintCopyPhase Phase { get; private set; } = BlueprintCopyPhase.SelectingStart;
        public Vector3Int StartCell { get; private set; }
        public Vector3Int EndCell { get; private set; }

        public void SelectStart(Vector3Int cell)
        {
            if (Phase != BlueprintCopyPhase.SelectingStart) throw new InvalidOperationException($"SelectStart in {Phase}");
            StartCell = cell;
            Phase = BlueprintCopyPhase.SelectingEnd;
        }

        public void SelectEnd(Vector3Int cell)
        {
            if (Phase != BlueprintCopyPhase.SelectingEnd) throw new InvalidOperationException($"SelectEnd in {Phase}");
            EndCell = cell;
            Phase = BlueprintCopyPhase.AwaitingName;
        }

        // 名前入力を閉じても始点を残す
        // Keep the start when the name dialog is cancelled
        public void ReturnToEndSelection()
        {
            if (Phase != BlueprintCopyPhase.AwaitingName) throw new InvalidOperationException($"ReturnToEndSelection in {Phase}");
            Phase = BlueprintCopyPhase.SelectingEnd;
        }

        public void BeginCreate()
        {
            if (Phase != BlueprintCopyPhase.AwaitingName) throw new InvalidOperationException($"BeginCreate in {Phase}");
            Phase = BlueprintCopyPhase.Creating;
        }

        public void Clear()
        {
            Phase = BlueprintCopyPhase.SelectingStart;
        }

        public static (Vector3Int min, Vector3Int max) CalcBox(Vector3Int a, Vector3Int b)
        {
            return (Vector3Int.Min(a, b), Vector3Int.Max(a, b));
        }
    }
}
