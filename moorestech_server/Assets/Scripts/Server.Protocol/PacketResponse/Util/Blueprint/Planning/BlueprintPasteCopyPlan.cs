using System.Collections.Generic;
using Game.Blueprint;

namespace Server.Protocol.PacketResponse.Util.Blueprint.Planning
{
    public enum BlueprintPasteCopyState { Placeable, GroundNotFound, AllOverlapped, NotUnlocked, MaterialShortage, NoResolvedBlocks, InvalidCoordinates }

    public class BlueprintPasteCopyPlan
    {
        public BlueprintPasteCopyDraft Draft { get; }
        public BlueprintPasteCopyState State { get; }
        public bool IsPlaced => State == BlueprintPasteCopyState.Placeable;

        public BlueprintPasteCopyPlan(BlueprintPasteCopyDraft draft, BlueprintPasteCopyState state)
        {
            Draft = draft;
            State = state;
        }

        // 重ならない要素だけを設置候補として返す
        // Enumerate only non-overlapping placement candidates
        public IEnumerable<BlueprintPlacementElement> EnumerateElementsToPlace()
        {
            for (var i = 0; i < Draft.Elements.Count; i++)
            {
                if (Draft.NonOverlapFlags[i]) yield return Draft.Elements[i];
            }
        }
    }
}
