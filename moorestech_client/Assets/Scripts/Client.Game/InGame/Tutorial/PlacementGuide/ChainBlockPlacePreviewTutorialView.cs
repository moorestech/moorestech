using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.ChainPreview;

namespace Client.Game.InGame.Tutorial.PlacementGuide
{
    /// <summary>
    ///     連結レイアウト1適用分のview
    ///     View for one chain layout application; completion removes only its own guid's definition from the shared state
    /// </summary>
    public class ChainBlockPlacePreviewTutorialView : ITutorialView
    {
        private readonly ChainPlacePreviewState _state;
        private readonly Guid _tutorialGuid;
        
        public ChainBlockPlacePreviewTutorialView(ChainPlacePreviewState state, Guid tutorialGuid)
        {
            _state = state;
            _tutorialGuid = tutorialGuid;
        }
        
        public void CompleteTutorial()
        {
            _state.Clear(_tutorialGuid);
        }
    }
}
