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
            int id_ = MachineRecipeConfig.GetRecipeData(id).ItemInputs[0].ItemId;
            Assert.AreEqual(ans,id_);
        }
    }
}