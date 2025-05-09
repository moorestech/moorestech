using System;
using System.Collections.Generic;
using System.Threading;
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
            foreach (var textInfo in movieParameter.Sequence.SequenceTextInfos)
            {
                tasks.Add(PlayTextSequence(textInfo));
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
            }
            
            async UniTask PlayTextSequence(MovieSequenceTextInfo textInfo)
            {
                await DelayTime(textInfo.SpawnTime);
                var prefab = movieParameter.Sequence.TextPrefab;
                var text = Instantiate(prefab.gameObject, transform);
                text.GetComponent<GameObjectMovieText>().SetText(textInfo.Text);
                
                text.transform.localPosition = textInfo.Position;
                text.transform.localRotation = Quaternion.Euler(textInfo.Rotation);
                text.transform.localScale = textInfo.Scale;
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