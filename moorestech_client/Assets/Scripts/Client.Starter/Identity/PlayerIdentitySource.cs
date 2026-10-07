namespace Client.Starter.Identity
{
    // 身元の出どころ。boolだと呼び出し側で真偽の意味が読めない
    // Where the identity comes from; a bool would leave the meaning of true and false unreadable at the call site
    public enum PlayerIdentitySource
    {
        SteamDistribution,
        Device,
    }
}
