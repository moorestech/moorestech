using System.Collections.Generic;
using System.Linq;
using industrialization.Config;
using industrialization.Item;
using industrialization_tdd.Config.Recipe;
using NUnit.Framework;

namespace industrialization.Test
{
    public class MachineRecipeConfigTest
    {
        [SetUp]
        public void Setup()
        {
        }

        [TestCase(0,1)]
        [TestCase(1,1.5)]
        public void RecipeTimeTest(int id,double ans)
        {
            double time = MachineRecipeConfig.GetRecipeData(id).Time;
            Assert.AreEqual(ans,time);
        }

        
        [TestCase(0,0)]
        [TestCase(1,2)]
        public void RecipeInputItemIdTest(int id, int ans)
        {
            int id_ = MachineRecipeConfig.GetRecipeData(id).ItemInputs[0].ID;
            Assert.AreEqual(ans,id_);
        }
        
        /// <summary>
        /// レシピがある時のテスト
        /// </summary>
        [TestCase(0,new int[1]{0},1)]
        [TestCase(0,new int[1]{2},1)]
        [TestCase(1,new int[2]{1,2},3)]
        [TestCase(1,new int[2]{2,1},3)]
        [TestCase(3,new int[3]{1,2,0},5)]
        [TestCase(3,new int[3]{2,1,0},5)]
        public void RecipeInputItemInstallationIDTest(int installtionID, int[] items,int output0ID)
        {
            var input = new List<IItemStack>();
            items.ToList().ForEach(
                i => input.Add(new ItemStack(i,1)));
            
            int ans = MachineRecipeConfig.GetRecipeData(installtionID, input).ItemOutputs[0].OutputItem.ID;
            Assert.AreEqual(output0ID,ans);
        }
    }
}