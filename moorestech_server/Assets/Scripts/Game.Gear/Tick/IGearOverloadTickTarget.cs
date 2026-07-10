namespace Game.Gear.Tick
{
    // 過負荷破断チェックをGearTickUpdaterから毎tick駆動するための口。block単位のUpdate購読は持たない。
    // 実装側はGearNetworkDatastoreへ登録/解除し、導出値(原点RPM比×原点RPM)で超過判定と確率破断を行う。
    // Hook for overload breakage checks driven every tick by GearTickUpdater; targets hold no per-block Update subscription.
    // Implementors register with GearNetworkDatastore and judge overload from derived values (signed ratio × origin RPM).
    public interface IGearOverloadTickTarget
    {
        void TickOverloadCheck();
    }
}
