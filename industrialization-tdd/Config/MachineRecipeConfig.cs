using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace industrialization.Config
{
    public class MachineRecipeConfig
    {
        
        private const string configPath = "C:\\Users\\satou\\RiderProjects\\industrialization-tdd\\industrialization-tdd\\Config\\Json\\macineRecipe.json";
        private static MachineRecipeData[] _recipeDatas;

        public static MachineRecipeData GetRecipeData(int id)
        {
            if (_recipeDatas == null)
            {
                _recipeDatas = loadConfig();
            }
            return _recipeDatas[id];
        }

        static MachineRecipeData[] loadConfig()
        {
            //JSONデータの読み込み
            var json = File.ReadAllText(configPath);
            var ms = new MemoryStream(Encoding.UTF8.GetBytes((json)));
            ms.Seek(0, SeekOrigin.Begin);
            var serializer = new DataContractJsonSerializer(typeof(MacineRecipe));
            var data = serializer.ReadObject(ms) as MacineRecipe;
            return data.Recipes;
        }
    }
    
    [DataContract] 
    class MacineRecipe
    {
        [DataMember(Name = "recipes")]
        private MachineRecipeData[] recipes;

        public MachineRecipeData[] Recipes => recipes;
    }

    [DataContract] 
    public class MachineRecipeData
    {
        [DataMember(Name = "installationID")]
        private int installationID;
        [DataMember(Name = "time")]
        private double time;
        [DataMember(Name = "input")]
        private MacineRecipeInput[] itemInputs;
        [DataMember(Name = "output")]
        private MacineRecipeOutput[] itemOutputs;

        public MacineRecipeOutput[] ItemOutputs => itemOutputs;

        public MacineRecipeInput[] ItemInputs => itemInputs;

        public double Time => time;

        public int InstallationId => installationID;
    }

    [DataContract] 
    public class MacineRecipeInput
    {
        [DataMember(Name = "id")]
        private int itemID;
        [DataMember(Name = "amount")]
        private int amount;

        public int Amount => amount;

        public int ItemId => itemID;
    }

    [DataContract] 
    public class MacineRecipeOutput
    {
        [DataMember(Name = "id")]
        private int itemID;
        [DataMember(Name = "amount")]
        private int amount;
        [DataMember(Name = "percent")]
        private double percent;

        public double Percent => percent;

        public int Amount => amount;

        public int ItemId => itemID;
    }
}