using MainGame.Basic;
using MainGame.UnityView.UI.Tutorial;

namespace MainGame.Presenter.Tutorial.ExecutableTutorials
{
    public class _1_SimpleMinerCraftTutorial : IExecutableTutorial
    {
        public bool IsFinishTutorial { get; private set; }
        private readonly HighlightRecipeViewerItem _highlightRecipeViewerItem;
        
        public _1_SimpleMinerCraftTutorial(HighlightRecipeViewerItem highlightRecipeViewerItem)
        {
            _highlightRecipeViewerItem = highlightRecipeViewerItem;
        }
        
        
        public void StartTutorial()
        {
            //TODO ここがmodのロード前に呼び出されるとバグるので修正する そもそもここが動く時データがロードされていないのが問題であるので、設計を変更する必要がある
            _highlightRecipeViewerItem.SetHighLight(BaseMod.ModId,"iron ingot",true);
        }

        public void Update()
        {
        }

        public void EndTutorial()
        {
        }
    }
}