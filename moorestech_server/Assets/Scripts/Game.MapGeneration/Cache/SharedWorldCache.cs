using Game.Paths;

namespace Game.MapGeneration.Cache
{
    // 生成システムの共有キャッシュ。同一PCで先焼き/クライアント焼きが共有
    // The generation system's shared cache, shared by the prebake and client bake on one PC
    public static class SharedWorldCache
    {
        public static WorldDataDirectory For(string worldId)
        {
            return WorldDataDirectory.FromWorldRoot(GameSystemPaths.GetWorldCacheDirectory(worldId));
        }
    }
}
