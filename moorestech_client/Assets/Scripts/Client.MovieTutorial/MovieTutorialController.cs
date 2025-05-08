using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.MovieTutorial
{
    public class MovieTutorialController : MonoBehaviour, IMovieTutorialController
    {
        public RenderTexture MovieRenderTexture => movieRenderTexture;
        
        [SerializeField] private Camera movieCamera;
        [SerializeField] private RenderTexture movieRenderTexture;
        
        [SerializeField] private float tutorialTime;
        
        [SerializeField] private Animator animator;
        
        public async UniTask PlayMovie(CancellationToken token)
        {
            animator.Play("PlayMovie");
            await UniTask.Delay(TimeSpan.FromSeconds(tutorialTime));
            
        }
        public void DestroyTutorial()
        {
        }
    }
}