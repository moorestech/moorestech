using MainGame.GameLogic.Chunk;
using MainGame.UnityView.Chunk;
using NUnit.Framework;
using UnityEditor;

namespace Test.EditModeTest.UnityView
{
    public class ChunkBlockGameObjectDataStoreTest
    {
        [Test]
        public void PlaceAndRemoveEventTest()
        {
            var dataStore = new ChunkBlockGameObjectDataStore();
            var blockUpdateEvent = new BlockUpdateEvent();
            var blocks = AssetDatabase.LoadAssetAtPath<BlockObjects>("Assets/ScriptableObject/BlockObjects.asset");
            dataStore.Construct(blockUpdateEvent,blocks);
            
                
        }
    }
}