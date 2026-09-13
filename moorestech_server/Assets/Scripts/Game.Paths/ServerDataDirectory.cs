using System.IO;

namespace Game.Paths
{
    /// <summary>サーバーがマスタとmodを読む場所の値オブジェクト。読み手と記録側が同じ値を指すための唯一の出所</summary>
    /// <summary>Value object for where the server reads masters and mods; the single source shared by its readers and the recorder</summary>
    public class ServerDataDirectory
    {
        public string Root { get; }

        // modの置き場。マスタのロード元とバグ報告が記録する場所を同じ値から導く
        // Where mods live; both the master load and the bug-report record derive from this one value
        public string ModsDirectory { get; }

        public ServerDataDirectory(string root)
        {
            Root = root;
            ModsDirectory = Path.Combine(root, "mods");
        }
    }
}
