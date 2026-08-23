namespace Game.MapGeneration.Transfer
{
    // world.jsonとワイヤが共有するmapMode文字列の唯一の定義。起動引数・プロビジョナ・クライアントもここを参照する
    // The single definition of the mapMode strings shared by world.json and the wire; boot args, the provisioner, and the client all reference these
    public static class WorldMapMode
    {
        public const string Template = "template";
        public const string Generated = "generated";
    }
}
