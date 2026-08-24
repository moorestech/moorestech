namespace Game.MapGeneration.Transfer
{
    // world.jsonとワイヤが共有するgeneratorVersionの唯一の定義。転送するファイル構成が変わるたび上げる
    // The single definition of the generatorVersion shared by world.json and the wire; bump it whenever the transferred file layout changes
    public static class WorldGeneratorVersion
    {
        public const string Current = "4.0.0";
    }
}
