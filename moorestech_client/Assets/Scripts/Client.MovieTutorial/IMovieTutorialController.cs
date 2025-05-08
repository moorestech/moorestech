using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.MovieTutorial
{
    public interface IMovieTutorialController
    {
        public UniTask PlayMovie(RenderTexture renderTexture, CancellationToken token = default);
        
        public void DestroyTutorial();
    }
}