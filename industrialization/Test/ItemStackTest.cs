using industrialization.Item;
using NUnit.Framework;

namespace industrialization.Test
{
    public class ItemStackTest
    {
        [SetUp]
        public void Setup()
        {
        }

        [TestCase(0,1,0,1,2,0,0,-1)]
        [TestCase(0,5,0,1,6,0,0,-1)]
        [TestCase(-1,0,1,3,3,0,1,-1)]
        [TestCase(-1,0,2,9,9,0,2,-1)]
        [TestCase(-1,5,0,1,1,0,0,-1)]
        [TestCase(0,1,-1,0,1,0,0,-1)]
        [TestCase(0,1,-1,0,1,0,0,-1)]
        [TestCase(0,5,-1,0,5,0,0,-1)]
        [TestCase(1,1,0,8,1,8,1,0)]
        [TestCase(1,1,0,1,1,1,1,0)]
        [TestCase(2,5,5,3,5,3,2,5)]
        public void AddTest(int mid,int mamo,int rid,int ramo,int ansMAmo,int ansRAmo,int ansMid,int ansRID)
        {
            IItemStack mineItemStack;
            if (mid == -1)
            {
                mineItemStack = new NullItemStack();
            }
            else
            {
                mineItemStack = new ItemStack(mid,mamo);
            }
            IItemStack receivedItemStack;
            if (rid == -1)
            {
                receivedItemStack = new NullItemStack();
            }
            else
            {
                receivedItemStack = new ItemStack(rid,ramo);
            }
            var result = mineItemStack.AddItem(receivedItemStack);
            Assert.AreEqual(result.MineItemStack.Amount, ansMAmo);
            Assert.AreEqual(result.ReceiveItemStack.Amount, ansRAmo);
            Assert.AreEqual(result.MineItemStack.ID, ansMid);
            Assert.AreEqual(result.ReceiveItemStack.ID, ansRID);
        }

        [TestCase(0,5,1,4,0)]
        [TestCase(-1,5,1,0,-1)]
        [TestCase(0,5,10,5,0)]
        [TestCase(0,8,8,0,-1)]
        public void SubTest(int mid, int mamo, int subamo, int ansamo, int ansID)
        {
            IItemStack mineItemStack;
            if (mid == -1)
            {
                mineItemStack = new NullItemStack();
            }
            else
            {
                mineItemStack = new ItemStack(mid,mamo);
            }

            var result = mineItemStack.SubItem(subamo);            
            Assert.AreEqual(ansamo,result.Amount);
            Assert.AreEqual(ansID,result.ID);

        }
        
    }
}