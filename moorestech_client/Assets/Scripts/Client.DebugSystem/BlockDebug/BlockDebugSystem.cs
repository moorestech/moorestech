using Client.Game.GameDebug;
using Client.Game.InGame.Block;
using UniRx;
using UnityEngine;

namespace Client.DebugSystem.BlockDebug
{
    public class BlockDebugSystem
    {
        public void Initialize()
        {
            DebugInfoStore.OnClickBlock.Subscribe(OnClickBlock);
        }
        
        public void OnClickBlock(BlockGameObject block)
        {
            Debug.Log($"Block clicked: {block}");
        }
    }
}