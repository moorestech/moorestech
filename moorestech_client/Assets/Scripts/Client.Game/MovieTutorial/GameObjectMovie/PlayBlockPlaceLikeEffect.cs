using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.MovieTutorial.GameObjectMovie.Objects;
using UnityEngine;

namespace Client.Game.MovieTutorial.GameObjectMovie
{
    /// <summary>
    /// ブロック設置した風のエフェクトを再生するコンポーネント
    /// </summary>
    public class PlayBlockPlaceLikeEffect : MonoBehaviour, IGameObjectMovieObject
    {
        [SerializeField] private BlockShaderAnimation blockShaderAnimation;
        
        public void SetParameters(List<string> parameters)
        {
            
        }
    }
}