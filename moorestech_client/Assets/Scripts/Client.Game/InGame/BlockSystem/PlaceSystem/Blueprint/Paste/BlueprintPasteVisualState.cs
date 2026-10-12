using Server.Protocol.PacketResponse.Util.Blueprint.Planning;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Paste
{
    internal static class BlueprintPasteVisualState
    {
        internal static bool Matches(BlueprintPastePlan previous, BlueprintPastePlan current)
        {
            if (previous == null || previous.Copies.Count != current.Copies.Count) return false;
            for (var copyIndex = 0; copyIndex < current.Copies.Count; copyIndex++)
            {
                var before = previous.Copies[copyIndex];
                var after = current.Copies[copyIndex];
                if (before.IsPlaced != after.IsPlaced || !MatchesDraft(before.Draft, after.Draft)) return false;
            }

            return true;
        }

        private static bool MatchesDraft(BlueprintPasteCopyDraft before, BlueprintPasteCopyDraft after)
        {
            if (before.Elements.Count != after.Elements.Count || before.Lines.Count != after.Lines.Count) return false;

            // 座標・向き・重なり色の変更だけを描画へ反映する
            // Repaint when block transforms or overlap colors change
            for (var index = 0; index < after.Elements.Count; index++)
            {
                var a = before.Elements[index];
                var b = after.Elements[index];
                if (a.BlockId != b.BlockId || a.Position != b.Position || a.Direction != b.Direction ||
                    before.NonOverlapFlags[index] != after.NonOverlapFlags[index]) return false;
            }

            // 接続可否と端点変更も描画の更新条件に含める
            // Include endpoint and connection eligibility changes in visual invalidation
            for (var index = 0; index < after.Lines.Count; index++)
            {
                var a = before.Lines[index];
                var b = after.Lines[index];
                if (a.Kind != b.Kind || a.ElementIndexA != b.ElementIndexA || a.ElementIndexB != b.ElementIndexB ||
                    a.PositionA != b.PositionA || a.PositionB != b.PositionB || a.IsConnectable != b.IsConnectable) return false;
            }

            return true;
        }
    }
}
