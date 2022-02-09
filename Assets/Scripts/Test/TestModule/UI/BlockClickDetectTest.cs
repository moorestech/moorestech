using MainGame.Control.Game;
using UnityEngine;

namespace Test.TestModule.UI
{
    public class BlockClickDetectTest : MonoBehaviour,IBlockClickDetect
    {
        public bool IsBlockClicked()
        {
            return Input.GetKeyDown(KeyCode.J);
        }
    }
}