using System.Collections.Generic;

namespace Client.Tests.UnitTest.CiShard.SourceScan
{
    // 走査結果は「持ち主を解決できた出現」と「読み違えた出現」に型で分け、センチネル文字列で混ぜない
    // Scan results split by type into resolved owners and misread occurrences instead of mixing them via sentinel strings
    public class PlayModeMethodScanResult
    {
        public List<ResolvedPlayModeMethod> ResolvedMethods { get; } = new();
        public List<MisreadPlayModeToken> Misreads { get; } = new();
    }

    public class ResolvedPlayModeMethod
    {
        public string Path { get; }
        // ネスト型はOuter+Inner、ジェネリック型はName`Arityで、Assembly.GetTypeがそのまま引ける形
        // Nested types use Outer+Inner and generics Name`Arity, the form Assembly.GetType resolves directly
        public string TypeFullName { get; }
        public string MethodName { get; }

        public ResolvedPlayModeMethod(string path, string typeFullName, string methodName)
        {
            Path = path;
            TypeFullName = typeFullName;
            MethodName = methodName;
        }
    }

    public class MisreadPlayModeToken
    {
        public string Path { get; }
        public int Line { get; }
        public string Reason { get; }

        public MisreadPlayModeToken(string path, int line, string reason)
        {
            Path = path;
            Line = line;
            Reason = reason;
        }

        public override string ToString()
        {
            return $"{Path}:{Line}: {Reason}";
        }
    }
}
