using System;
using System.Runtime.Serialization;
using industrialization.Item;

namespace industrialization.Config.Recipe.Json
{
    [DataContract] 
    public class PurseJsonMachineRecipes
    {
        
        [DataMember(Name = "recipes")]
        private MachineRecipe[] _recipes;

        public MachineRecipe[] Recipes => _recipes;
    }

    [DataContract] 
    public class MachineRecipe
    {
        [DataMember(Name = "installationID")]
        private int _installationId;
        [DataMember(Name = "time")]
        private int _time;
        [DataMember(Name = "input")]
        private MachineRecipeInput[] _itemInputs;
        [DataMember(Name = "output")]
        private MachineRecipeOutput[] _itemOutputs;

        public MachineRecipeOutput[] ItemOutputs => _itemOutputs;

        public MachineRecipeInput[] ItemInputs => _itemInputs;

        public int Time => _time;

        public int InstallationId => _installationId;
    }

    [DataContract] 
    public class MachineRecipeInput
    {
        [DataMember(Name = "id")]
        private int _itemId;
        [DataMember(Name = "amount")]
        private int _amount;


        public ItemStack ItemStack => new ItemStack(_itemId, _amount);
    }

    [DataContract] 
    public class MachineRecipeOutput
    {
        [DataMember(Name = "id")]
        private int _itemId;
        [DataMember(Name = "amount")]
        private int _amount;
        [DataMember(Name = "percent")]
        private double _percent;

        public double Percent => _percent;

        public ItemStack ItemStack => new ItemStack(_itemId, _amount);
    }
}