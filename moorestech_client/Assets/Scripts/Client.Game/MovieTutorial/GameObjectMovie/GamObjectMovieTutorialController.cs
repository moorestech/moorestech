using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Game.MovieTutorial.GameObjectMovie
{
    public class GamObjectMovieTutorialController : MonoBehaviour, IMovieTutorialController
    {
        [SerializeField] private Camera movieCamera;
        
        [SerializeField] private float tutorialTime;
        [SerializeField] private Animator animator;
        
        public async UniTask PlayMovie(IMovieTutorialParameter parameter, RenderTexture renderTexture, CancellationToken token = default)
        {
            movieCamera.targetTexture = renderTexture;
            
            await DelayTime(tutorialTime);
            
            #region Internal
            
            async UniTask PlaySequence(MovieSequenceInfo sequenceInfo)
            {
                await DelayTime(sequenceInfo.SpawnTime);
                var prefab = sequenceInfo.Prefab;
                var sequenceObject = Instantiate(prefab, transform);
                
                sequenceObject.transform.localPosition = sequenceInfo.Position;
                sequenceObject.transform.localRotation = Quaternion.Euler(sequenceInfo.Rotation);
                sequenceObject.transform.localScale = sequenceInfo.Scale;
            }
            
            async UniTask DelayTime(float second)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(second), cancellationToken: token);
            }
            
            #endregion
        }
        
        
        public void DestroyTutorial()
        {
            Destroy(gameObject);
        }
    }
}