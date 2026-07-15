namespace Client.Game.InGame.Control
{
    public interface IPlayerCameraInteractionApplier
    {
        void SetCursorVisible(bool visible);
        void SetCameraRotatable(bool rotatable);
    }
}
