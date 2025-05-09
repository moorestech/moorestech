using System;
using Client.Game.InGame.Block;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Game.MovieTutorial.GameObjectMovie
{
    /// <summary>
    /// ブロック設置した風のエフェクトを再生するコンポーネント
    /// </summary>
    public class PlayBlockPlaceLikeEffect : MonoBehaviour
    {
        [SerializeField] private BlockShaderAnimation blockShaderAnimation;
        [SerializeField] private bool playPlaceAnimation;
        [SerializeField] private bool playRemoveAnimation;
        
        private void Update()
        {
            if (playPlaceAnimation)
            {
                PlaceAnimation();
            }
            
            if (playRemoveAnimation)
            {
                RemoveAnimation();
            }
        }
        
        public void PlaceAnimation()
        {
            blockShaderAnimation.PlaceAnimation().Forget();
        }
        
        public void RemoveAnimation()
        {
            blockShaderAnimation.RemoveAnimation().Forget();
        }
    }
}