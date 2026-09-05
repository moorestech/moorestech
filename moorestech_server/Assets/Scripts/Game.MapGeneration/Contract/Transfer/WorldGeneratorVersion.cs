using System;

namespace Game.MapGeneration.Transfer
{
    // world.jsonとワイヤが共有するgeneratorVersionの唯一の定義。転送するファイル構成が変わるたび上げる
    // The single definition of the generatorVersion shared by world.json and the wire; bump it whenever the transferred file layout changes
    public static class WorldGeneratorVersion
    {
        public const string Current = "4.0.0";

        // 版照合はpayloadを組むより先に通す。別ビルドのワイヤ値は必須項目そのものが欠けており、先に組むと版不一致の診断へ到達しない
        // The version check runs before a payload is built: another build's wire values lack the required fields themselves, so building first never reaches the version diagnosis
        public static void ThrowIfDiffers(string generatorVersion, string worldId)
        {
            if (generatorVersion == Current) return;
            throw new InvalidOperationException(
                $"Terrain transfer meta of world '{worldId}' was produced by generator '{generatorVersion}', " +
                $"but this build is '{Current}'. The transferred terrain file layout differs; connect to a server on the same build.");
        }
    }
}
