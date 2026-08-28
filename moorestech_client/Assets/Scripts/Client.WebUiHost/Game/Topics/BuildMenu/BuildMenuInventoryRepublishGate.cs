using Client.Game.InGame.UI.UIState;

namespace Client.WebUiHost.Game.Topics.BuildMenu
{
    /// <summary>
    /// 所持変化での再配信可否。閉じている間の所持変化は次の入場で拾い直す
    /// Whether an inventory change should republish; changes while closed are picked up on the next entry
    /// </summary>
    public class BuildMenuInventoryRepublishGate
    {
        private readonly UIStateControl _uiStateControl;

        public BuildMenuInventoryRepublishGate(UIStateControl uiStateControl)
        {
            _uiStateControl = uiStateControl;
        }

        public bool ShouldRepublish()
        {
            return _uiStateControl.CurrentState == UIStateEnum.BuildMenu;
        }
    }
}
