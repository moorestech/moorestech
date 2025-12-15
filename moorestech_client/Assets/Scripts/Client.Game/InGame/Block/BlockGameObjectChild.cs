using Client.Game.InGame.UI.UIState.State;
using UnityEngine;

namespace Client.Game.InGame.Block
{
    public class BlockGameObjectChild : MonoBehaviour, IDeleteTarget
    {
        public BlockGameObject BlockGameObject { get; private set; }
        
        public void Init(BlockGameObject blockGameObject)
        {
            BlockGameObject = blockGameObject;
        }
        
        public void SetRemovePreviewing()
        {
            BlockGameObject.SetRemovePreviewing();
        }
        
        public void ResetMaterial()
        {
            BlockGameObject.ResetMaterial();
        }
    }
}