namespace Game.Gear.Common
{
    // 歯車網での役割。正本はサーバーの IGearGenerator 実装有無
    // Role within a gear network; the server's IGearGenerator implementation is the authority
    public enum GearRole
    {
        Consumer,
        Generator,
    }
}
