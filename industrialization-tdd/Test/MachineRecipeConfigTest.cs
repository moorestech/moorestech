using System.Collections.Generic;
using System.Linq;
using industrialization.Config;
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
            int id_ = MachineRecipeConfig.GetRecipeData(id).ItemInputs[0].ItemStack.ID;
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
            int ans = MachineRecipeConfig.GetRecipeData(installtionID, items.ToList()).
            Assert.AreEqual(ans,output0ID);
        }
    }
}