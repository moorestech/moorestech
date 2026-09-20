using System;

namespace Game.MapGeneration.Transfer
{
    // world.jsonとワイヤが共有するmapMode文字列の唯一の定義。起動引数・プロビジョナ・クライアントもここを参照する
    // The single definition of the mapMode strings shared by world.json and the wire; boot args, the provisioner, and the client all reference these
    public static class WorldMapMode
    {
        public const string Template = "template";
        public const string Generated = "generated";

        // 生成ワールド判定。world.json の読み手・ワイヤの復元・バグ報告の書き手と再現側がここを通り、綴りの完全一致だけを生成ワールドとみなす
        // Decides "generated"; world.json readers, the wire restore, and the bug-report writer and reproducer go through here, and only the exact spelling counts
        public static bool IsGenerated(string mapMode)
        {
            return string.Equals(mapMode, Generated, StringComparison.Ordinal);
        }
    }
}
