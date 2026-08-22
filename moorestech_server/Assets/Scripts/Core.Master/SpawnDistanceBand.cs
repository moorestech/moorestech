namespace Core.Master
{
    // スポーン地点中心の同心円リングを1本表す帯の共通基底。鉱脈帯と散布帯が継承する。
    // 帯そのものをリングへ載せるため、リング計画の隣（Core.Master）に置く。
    // Common base for one concentric band around the spawn point; vein bands and scatter bands derive from it.
    // It sits next to the ring planner (Core.Master) so a ring can carry the band itself.
    public abstract class SpawnDistanceBand
    {
        // -1（負値）は無限（最外周）。
        // -1 (negative) means infinite (outermost ring).
        public float outerRadiusMeters = -1f;
    }
}
