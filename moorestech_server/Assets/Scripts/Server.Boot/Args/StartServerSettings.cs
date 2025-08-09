#nullable enable
using Server.Boot.Args;

namespace Server.Boot
{
    public class StartServerSettings
    {
        [Option(isFlag: false, "--saveFilePath", "-s")]
        public string SaveFilePath { get; set; } = MoorestechServerDIContainerOptions.DefaultSaveJsonFilePath;
        
        [Option(isFlag: false, "--autoSave", "-a")]
        public bool AutoSave { get; set; } = true;
        
        [Option(isFlag: false, "--serverDataDirectory")]
        public string ServerDataDirectory { get; set; } = ServerDirectory.GetDirectory();
    }
}