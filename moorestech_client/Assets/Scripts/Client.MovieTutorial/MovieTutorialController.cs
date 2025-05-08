using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.MovieTutorial
{
    public class MovieTutorialController : MonoBehaviour, IMovieTutorialController
    {
        [SerializeField] private Camera movieCamera;
        
        [SerializeField] private float tutorialTime;
        
        [SerializeField] private Animator animator;
        
        public async UniTask PlayMovie(RenderTexture renderTexture, CancellationToken token = default)
        {
            movieCamera.targetTexture = renderTexture;
            
            animator.Play("PlayMovie");
            await UniTask.Delay(TimeSpan.FromSeconds(tutorialTime));
        }
        
        public void DestroyTutorial()
        {
            Destroy(gameObject);
        }
    }
}