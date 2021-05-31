using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace industrialization.Core.Config.Installation
{
    public static class InstallationConfig
    {
        private static InstallationData[] _machineDatas;

        public static InstallationData GetInstallationsConfig(int id)
        {
            _machineDatas ??= LoadJsonFile();

            return _machineDatas[id];
        }

        private static InstallationData[] LoadJsonFile()
        {
            var json = File.ReadAllText(ConfigPath.InstallationConfigPath);
            var ms = new MemoryStream(Encoding.UTF8.GetBytes((json)));
            ms.Seek(0, SeekOrigin.Begin);
            var serializer = new DataContractJsonSerializer(typeof(InstallationJson));
            var data = serializer.ReadObject(ms) as InstallationJson;
            return data?.Installations;
        }
    }

    [DataContract] 
    public class InstallationData
    {
        [DataMember(Name = "name")]
        private string _name;

        [DataMember(Name = "inputSlot")]
        private int _inputSlot;
        
        [DataMember(Name = "outputSlot")]
        private int _outputSlot;
        
        public string Name => _name;

        public int InputSlot => _inputSlot;
        public int OutputSlot => _outputSlot;
    }
}