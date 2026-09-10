namespace Client.Game.InGame.Interact.Selection
{
    /// <summary>
    ///     レイが当たるコライダに付ける、インタラクト対象への案内
    ///     Marker on a collider hit by the ray that points at its interactable
    /// </summary>
    public interface IInteractRayTarget
    {
        IInteractable Interactable { get; }
    }
}
