#nullable enable
using Game.MapGeneration.Provisioning;
using Game.Paths;
using Server.Boot.Args;

namespace Server.Boot
{
    public class StartServerSettings
    {
        // ワールドディレクトリのルート。世界ごとの全ファイルがこの配下に置かれる
        // Root of the world directory; every per-world file lives under this path
        [Option(isFlag: false, "--worldDirectory", "-w")]
        public string WorldDirectory { get; set; } = GameSystemPaths.DefaultWorldDirectory;

        // ワールド新規作成時の生成モード（"template" | "generated"）
        // Provisioning mode for a fresh world ("template" | "generated")
        [Option(isFlag: false, "--mapMode")]
        public string MapMode { get; set; } = WorldProvisioner.TemplateMapMode;

        // generatedモードのシード。未指定(null)なら196を使い、0も有効なseed値として扱う
        // Seed for generated mode; an unspecified value (null) resolves to 196, while zero remains valid
        [Option(isFlag: false, "--seed")]
        public int? Seed { get; set; } = null;

        // 待ち受けポート。未指定(null)なら既定ポート、0ならOSが空きポートを採番する
        // Listen port; default port when unspecified (null), OS assigns a free port when 0
        [Option(isFlag: false, "--port", "-p")]
        public int? Port { get; set; } = null;

        [Option(isFlag: false, "--autoSave", "-a")]
        public bool AutoSave { get; set; } = true;

        [Option(isFlag: false, "--serverDataDirectory")]
        public string ServerDataDirectory { get; set; } = ServerDirectory.GetDirectory();
    }
}
