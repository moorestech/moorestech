namespace Game.Block.Interface.Component
{
    // 部屋の不純物を除去する供給源（エアフィルター）。CleanRoomDatastore が n·q 集計と摩耗プッシュに使う。
    // Impurity-removal source (air filter); CleanRoomDatastore reads n·q and pushes wear through this.
    public interface ICleanRoomAirFilter : IBlockComponent
    {
        // 満電時 q × 電力割合(≤1) × (フィルター残>0 ? 1 : 0)。n·q の自台寄与。
        // q × power-ratio(≤1) × (filter present ? 1 : 0); this unit's contribution to n·q.
        double RemovalVolumePerSecond { get; }

        // データストアがこの台の今tickの除去不純物量を渡す。フィルター摩耗に使う。
        // Datastore pushes this unit's removed impurity for the tick; drives filter wear.
        void ApplyRemovedImpurity(double removed);
    }
}
