using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Game.MovieTutorial
{
    public interface IMovieTutorialController
    {
        public UniTask PlayMovie(IMovieTutorialParameter parameter, RenderTexture renderTexture, CancellationToken token = default);
        
        public void DestroyTutorial();
    }
}