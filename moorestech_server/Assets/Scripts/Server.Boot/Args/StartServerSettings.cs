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
        public string WorldDirectory { get; set; } = GameSystemPaths.GetSaveFilePath("world_1");

        // ワールド新規作成時の生成モード（"template" | "generated"）
        // Provisioning mode for a fresh world ("template" | "generated")
        [Option(isFlag: false, "--mapMode")]
        public string MapMode { get; set; } = WorldProvisioner.TemplateMapMode;

        // generatedモードのシード。未指定(null)なら生成時に採番する。0も有効なseed値として扱う
        // Seed for generated mode; assigned at provisioning time only when unspecified (null). 0 is a valid seed
        [Option(isFlag: false, "--seed")]
        public int? Seed { get; set; } = null;

        [Option(isFlag: false, "--autoSave", "-a")]
        public bool AutoSave { get; set; } = true;

        [Option(isFlag: false, "--serverDataDirectory")]
        public string ServerDataDirectory { get; set; } = ServerDirectory.GetDirectory();
    }
}
