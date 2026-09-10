namespace Client.Game.InGame.Tutorial
{
    /// <summary>
    ///     絶対座標ゴースト1適用分のview
    ///     View for one absolute-position ghost application; completing it folds only its own guid via the manager
    /// </summary>
    public class BlockPlacePreviewTutorialView : ITutorialView
    {
        private readonly BlockPlacePreviewTutorialManager _manager;
        private readonly string _tutorialGuid;
        
        public BlockPlacePreviewTutorialView(BlockPlacePreviewTutorialManager manager, string tutorialGuid)
        {
            _manager = manager;
            _tutorialGuid = tutorialGuid;
        }
        
        public void CompleteTutorial()
        {
            _manager.Complete(_tutorialGuid);
        }
    }
}
