namespace Client.Game.InGame.Tutorial.UIHighlight
{
    // マスタのanchorIdは無変換でWebへ渡す方針のため、実行時導出が必要なitemIdだけをここで組み立てる
    // Master anchorIds pass through to the web verbatim; only runtime-derived itemId anchors are built here
    public static class TutorialAnchorIdMapper
    {
        // 動的アンカーIDのprefix。Web側TutorialAnchorDynamicPrefixesと対応する
        // Dynamic anchor ID prefix; must mirror Web's TutorialAnchorDynamicPrefixes
        public const string ItemAnchorPrefix = "recipe.item-";

        public static string FromItemId(int itemId)
        {
            return $"{ItemAnchorPrefix}{itemId}";
        }
    }
}
