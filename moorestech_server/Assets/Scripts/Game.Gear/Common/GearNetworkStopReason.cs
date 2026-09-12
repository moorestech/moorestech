namespace Game.Gear.Common
{
    public enum GearNetworkStopReason
    {
        None,
        Rocked,
        OverRequirePower,

        // 発電機はいるが供給が0（燃料切れ・停止中）。網全体が止まる
        // Generators exist but supply nothing (out of fuel / halted), so the whole network stops
        NoGeneration,

        // 網に発電機が1台も無い
        // The network contains no generator at all
        NoGenerator,
    }
}