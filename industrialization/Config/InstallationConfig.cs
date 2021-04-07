using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace industrialization.Config
{
    public static class InstallationConfig
    {
        private const string ConfigPath = "C:\\Users\\satou_katsumi\\RiderProjects\\industrialization\\industrialization\\Config\\Json\\installation.json";
        private static InstallationData[] _machineDatas;

        public static InstallationData GetInstallationsConfig(int id)
        {
            _machineDatas ??= LoadJsonFile();

            return _machineDatas[id];
        }

        private static InstallationData[] LoadJsonFile()
        {
            var json = File.ReadAllText(ConfigPath);
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

        [DataMember(Name = "inventorySlots")]
        private int _inventorySlot;
        
        public string Name => _name;

        public int InventorySlot => _inventorySlot;
    }
}