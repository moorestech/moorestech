using Game.Paths;

namespace Game.MapGeneration.Cache
{
    // 生成システムの共有キャッシュ。同一PCならサーバーの先焼きもクライアントの焼きも同じ場所へ落ちる
    // The generation system's shared cache; on one PC the server's prebake and the client's bake land in the same place
    public static class SharedWorldCache
    {
        public static WorldDataDirectory For(string worldId)
        {
            return WorldDataDirectory.FromWorldRoot(GameSystemPaths.GetWorldCacheDirectory(worldId));
        }
    }
}
