using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.MovieTutorial
{
    public interface IMovieTutorialController
    {
        public RenderTexture MovieRenderTexture { get; }
        public UniTask PlayMovie(CancellationToken token = default);
        
        public void DestroyTutorial();
    }
}