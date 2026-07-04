using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Parts
{
    /// <summary>
    /// 1フレーム分のプレビュー表示指示。Decideの出力であり、表示側から判断材料は一切返さない。
    /// Per-frame preview display command. Output of Decide; the view never returns judgement data back.
    /// </summary>
    public readonly struct GearChainPolePreviewCommand
    {
        public readonly bool GhostVisible;
        public readonly bool GhostPlaceable;
        public readonly bool LineVisible;
        public readonly Vector3 LineStart;
        public readonly Vector3 LineEnd;
        public readonly bool LinePlaceable;

        public static GearChainPolePreviewCommand Hidden => new(false, false, false, Vector3.zero, Vector3.zero, false);

        public static GearChainPolePreviewCommand Ghost(bool placeable)
        {
            return new GearChainPolePreviewCommand(true, placeable, false, Vector3.zero, Vector3.zero, false);
        }

        public static GearChainPolePreviewCommand Line(Vector3 start, Vector3 end, bool placeable)
        {
            return new GearChainPolePreviewCommand(false, false, true, start, end, placeable);
        }

        public static GearChainPolePreviewCommand GhostAndLine(bool placeable, Vector3 lineStart, Vector3 lineEnd)
        {
            return new GearChainPolePreviewCommand(true, placeable, true, lineStart, lineEnd, placeable);
        }

        private GearChainPolePreviewCommand(bool ghostVisible, bool ghostPlaceable, bool lineVisible, Vector3 lineStart, Vector3 lineEnd, bool linePlaceable)
        {
            GhostVisible = ghostVisible;
            GhostPlaceable = ghostPlaceable;
            LineVisible = lineVisible;
            LineStart = lineStart;
            LineEnd = lineEnd;
            LinePlaceable = linePlaceable;
        }
    }
}
