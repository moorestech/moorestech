using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace industrialization.Config
{
    public class InstallationConfig
    {
        private const string configPath = "C:\\Users\\satou_katsumi\\RiderProjects\\industrialization-tdd\\industrialization-tdd\\Config\\Json\\installation.json";
        private static InstallationData[] _machineDatas;

        public static InstallationData GetInstllationConfig(int id)
        {
            if (_machineDatas == null)
            {
                //JSONデータの読み込み
                _machineDatas = loadJsonFile();
            }

            return _machineDatas[id];
        }

        static InstallationData[] loadJsonFile()
        {
            var json = File.ReadAllText(configPath);
            var ms = new MemoryStream(Encoding.UTF8.GetBytes((json)));
            ms.Seek(0, SeekOrigin.Begin);
            var serializer = new DataContractJsonSerializer(typeof(installationJson));
            var data = serializer.ReadObject(ms) as installationJson;
            return data.Installations;
        }
    }

    [DataContract] 
    class installationJson
    {
        [DataMember(Name = "installations")]
        private InstallationData[] installations;

        public InstallationData[] Installations => installations;
    }
    [DataContract] 
    public class InstallationData
    {
        [DataMember(Name = "name")]
        private string name;

        [DataMember(Name = "inventorySlots")]
        private int inventorySlot;
        
        public string Name => name;

        public int InventorySlot => inventorySlot;
    }
}