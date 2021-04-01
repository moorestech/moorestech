using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace industrialization.Installation
{
    public class InstallationConfig
    {
        private const string configPath = "";
        private static InstallationData[] _machineDatas;

        public static InstallationData GetMachineData(int id)
        {
            if (_machineDatas == null)
            {
                //JSONデータの読み込み
                var json = File.ReadAllText(configPath);
                var ms = new MemoryStream(Encoding.UTF8.GetBytes((json)));
                ms.Seek(0, SeekOrigin.Begin);
                var serializer = new DataContractJsonSerializer(typeof(installationJson));
                var data = serializer.ReadObject(ms) as installationJson;
            }

            return null;
        }
        
    }

    [DataContract] 
    public class installationJson
    {
        [DataMember(Name = "installations")]
        public InstallationData[] installations;
    }
    public class InstallationData
    {
        [DataMember(Name = "name")]
        public string name;
        [DataMember(Name = "inventorySlots")]
        public int inventorySlot;
    }
}