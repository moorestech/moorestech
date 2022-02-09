using UnityEngine;

namespace MainGame.Control.Game
{
    public class BlockClickDetect : MonoBehaviour,IBlockClickDetect
    {
        public bool IsBlockClicked()
        {
            return true;
        }
    }
}