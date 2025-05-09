using System;
using System.Collections.Generic;
using System.Threading;
using Client.MovieTutorial.GameObjectMovie.Objects;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace Client.MovieTutorial.GameObjectMovie
{
    public class GamObjectMovieTutorialController : MonoBehaviour, IMovieTutorialController
    {
        [SerializeField] private Camera movieCamera;
        
        [SerializeField] private float tutorialTime;
        
        public async UniTask PlayMovie(IMovieTutorialParameter parameter, RenderTexture renderTexture, CancellationToken token = default)
        {
            movieCamera.targetTexture = renderTexture;
            
            var movieParameter = parameter as GameObjectMovieTutorialParameter;
            var tasks = new List<UniTask>();
            foreach (var sequenceInfo in movieParameter.Sequence.SequenceInfos)
            {
                tasks.Add(PlaySequence(sequenceInfo));
            }
            
            tasks.Add(DelayTime(tutorialTime));
            await UniTask.WhenAll(tasks);
            
            #region Internal
            
            async UniTask PlaySequence(MovieSequenceInfo sequenceInfo)
            {
                await DelayTime(sequenceInfo.SpawnTime);
                var prefab = sequenceInfo.Prefab;
                var sequenceObject = Instantiate(prefab, transform);
                
                sequenceObject.transform.localPosition = sequenceInfo.Position;
                sequenceObject.transform.localRotation = Quaternion.Euler(sequenceInfo.Rotation);
                sequenceObject.transform.localScale = sequenceInfo.Scale;
                
                sequenceObject.GetComponent<IGameObjectMovieObject>().SetParameters(sequenceInfo.Parameters);
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