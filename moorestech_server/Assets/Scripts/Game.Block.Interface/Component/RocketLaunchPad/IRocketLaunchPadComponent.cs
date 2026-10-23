namespace Game.Block.Interface.Component
{
    /// <summary>
    ///     建造・発射の合図を受ける発射台の部品。合図が意味を持つのはワールドの初打ち上げ前だけ
    ///     Launch pad component accepting build and launch commands; they only matter before the world's first launch
    /// </summary>
    public interface IRocketLaunchPadComponent : IBlockComponent
    {
        RocketLaunchPadCommandResult RequestBuild();
        RocketLaunchPadCommandResult RequestLaunch();
    }

    public enum RocketLaunchPadCommandResult
    {
        Accepted,
        NotAwaitingBuildCommand,
        NotAwaitingLaunchCommand,
        MissingMaterials,
        MissingPayload,
        OutputFull,
    }
}
