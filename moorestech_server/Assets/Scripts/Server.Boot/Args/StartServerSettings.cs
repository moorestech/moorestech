#nullable enable
using Server.Boot.Args;

namespace Server.Boot
{
    /// <summary>アプリ固有設定（例）。必要に応じて別 POCO を増やす。</summary>
    public class StartServerSettings
    {
        [Option(isFlag: false, "--saveFilePath", "-s")]
        public string SaveFilePath { get; set; } = MoorestechServerDIContainerOptions.DefaultSaveJsonFilePath;
        
        [Option(isFlag: true, "--autoSave", "-c")]
        public bool AutoSave { get; set; } = true;
    }
}