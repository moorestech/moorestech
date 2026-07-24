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

        // generatedモードのシード。0のままなら生成時に採番する
        // Seed for generated mode; assigned at provisioning time when left at 0
        [Option(isFlag: false, "--seed")]
        public int Seed { get; set; } = 0;

        [Option(isFlag: false, "--autoSave", "-a")]
        public bool AutoSave { get; set; } = true;

        [Option(isFlag: false, "--serverDataDirectory")]
        public string ServerDataDirectory { get; set; } = ServerDirectory.GetDirectory();
    }
}
