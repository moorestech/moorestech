using System.Collections;
using Client.Game.InGame.UI.Challenge;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static Client.Tests.PlayModeTest.Util.PlayModeTestUtil;
using Object = UnityEngine.Object;

namespace Client.Tests.PlayModeTest
{
    public class BeltConveyorItemObjectTest
    {
        [UnityTest]
        public IEnumerator BeltConveyorItem()
        {
            yield return new EnterPlayMode(expectDomainReload: true);
            
            yield return Test().ToCoroutine();
            
            yield return new ExitPlayMode();
            
            #region Internal
            
            async UniTask Test()
            {
                await LoadMainGame();
                
                // TODO このブロックの名前を英語にする
                // TODO Change the name of this block to English
                var chest = PlaceBlock("量子チェスト", Vector3Int.zero, BlockDirection.North);
                PlaceBlock("ベルトコンベア", new Vector3Int(0, 0, 1), BlockDirection.North);
                PlaceBlock("ベルトコンベア", new Vector3Int(0, 0, 2), BlockDirection.East);
                PlaceBlock("ベルトコンベア", new Vector3Int(1, 0, 2), BlockDirection.East);
                PlaceBlock("ベルトコンベア", new Vector3Int(2, 0, 2), BlockDirection.South);
                PlaceBlock("ベルトコンベア", new Vector3Int(2, 0, 1), BlockDirection.South);
                PlaceBlock("ベルトコンベア", new Vector3Int(2, 0, 0), BlockDirection.West);
                PlaceBlock("ベルトコンベア", new Vector3Int(1, 0, 0), BlockDirection.West);
                
                
                InsertItemToBlock(chest, new ItemId(1), 100);
            }
            
            #endregion
        }
    }
}