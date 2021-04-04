using System.Runtime.Serialization;

namespace industrialization.Config
{
    [DataContract] 
    public class MachineRecipe
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