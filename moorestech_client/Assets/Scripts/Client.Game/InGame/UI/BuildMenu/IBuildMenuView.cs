namespace Client.Game.InGame.UI.BuildMenu
{
    public interface IBuildMenuView
    {
        void SetActive(bool active);
        bool TryConsumeSelectedEntry(out BuildMenuEntry entry);
    }
}
