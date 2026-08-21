namespace Client.Game.InGame.Mining
{
    /// <summary>
    ///     採掘レイが当たるコライダに付ける、採掘対象への案内
    ///     Marker on a collider hit by the mining ray that points at its mining target
    /// </summary>
    public interface IMiningRayTarget
    {
        IMiningTargetObject MiningTargetObject { get; }
    }
}
