namespace Client.Ui.Screens.Tutorial.Anchor
{
    /// <summary>
    /// 測ったアンカーの地を引く（見つかっていないアンカーは false）
    /// Looks up the ground of a measured anchor (false for anchors not found)
    /// </summary>
    public interface ITutorialAnchorGroundLookup
    {
        bool TryGetGround(string anchorId, out TutorialAnchorGround ground);
    }
}
