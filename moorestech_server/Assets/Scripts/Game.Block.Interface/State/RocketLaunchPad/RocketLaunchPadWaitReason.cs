namespace Game.Block.Interface.State
{
    // 同じ段階でも何を待つかを表示へ伝える
    // Reports what the pad awaits within its current phase
    public enum RocketLaunchPadWaitReason
    {
        None = 0,
        MissingMaterials = 1,
        MissingPayload = 2,
        OutputFull = 3,
        NoPower = 4,
    }
}
