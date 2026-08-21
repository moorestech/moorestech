namespace Game.MapGeneration.Pipeline.Config
{
    // 配置物が地形の見た目へ効く種別。配置器が配置元エントリから写し、見た目ステージだけが読む
    // How a placement affects the terrain's look; the placer copies it from its source entry and only the visual stages read it
    public enum TerrainSurroundEffectType
    {
        treeRootPatch,
        rockBareGround,
        rockNoBareGround,
    }
}
