namespace Game.MapGeneration.Pipeline.Config
{
    // 配置物が地形の見た目へ効く種別
    // How a placement affects the terrain's look
    public enum TerrainSurroundEffectType
    {
        // 代入漏れの既定値0を実在種別と衝突させないための番兵。台帳へ来たら例外にする
        // A sentinel for the default value 0 so it never collides with a real effect; reaching the ledger throws
        none = 0,
        treeRootPatch,
        rockBareGround,
        rockNoBareGround,
    }
}
