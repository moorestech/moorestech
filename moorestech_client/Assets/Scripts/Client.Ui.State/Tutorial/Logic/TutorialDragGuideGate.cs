namespace Client.Ui.State.Tutorial.Logic
{
    /// <summary>
    /// ドラッグ誘導を出してよいかの門と移動量。Web の DragGuide の写し
    /// The gate deciding whether the drag guide shows, and its travel; a copy of Web's DragGuide
    /// </summary>
    public static class TutorialDragGuideGate
    {
        public static bool TryPlace(TutorialAnchorState from, TutorialAnchorState to, out TutorialDragGuidePlacement placement)
        {
            placement = default;
            // 両端が ready で、起点の中心が起点の clip の内側にある時だけ描く
            // Draw only when both ends are ready and the origin center lies inside the origin clip
            // silent-ok: 端が未解決・起点が見えない間は Web と同じく出さない仕様 / 未解決の理由は TutorialAnchorTracker が遷移時に出し、L1 TutorialOverlayDragGuideTest が見る
            if (from is not TutorialAnchorState.Ready origin || to is not TutorialAnchorState.Ready target) return false;
            if (!origin.Clip.Contains(origin.Rect.CenterX, origin.Rect.CenterY)) return false;
            placement = new TutorialDragGuidePlacement(origin.Rect.CenterX, origin.Rect.CenterY, target.Rect.CenterX - origin.Rect.CenterX, target.Rect.CenterY - origin.Rect.CenterY);
            return true;
        }
    }

    public readonly struct TutorialDragGuidePlacement
    {
        public readonly float OriginX;
        public readonly float OriginY;
        public readonly float TravelX;
        public readonly float TravelY;

        public TutorialDragGuidePlacement(float originX, float originY, float travelX, float travelY)
        {
            OriginX = originX;
            OriginY = originY;
            TravelX = travelX;
            TravelY = travelY;
        }
    }
}
