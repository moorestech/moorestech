namespace Game.World.Interface.DataStore
{
    // 設置・撤去で崩れた派生網(電力・歯車・流体・ベルト搬送)をワールドから作り直す。tick末尾の操作適用後とロード完了時に呼ぶ
    // Rebuilds the derived networks (electric, gear, fluid, belt transport) invalidated by placements and removals; called after tick-end input is applied and when a load completes
    public interface IWorldTopologyRebuilder
    {
        void RebuildTopologiesIfDirty();
    }
}
