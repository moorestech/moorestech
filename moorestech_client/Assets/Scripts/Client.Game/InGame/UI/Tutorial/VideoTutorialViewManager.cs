using Client.Common.Asset;
using Client.MovieTutorial;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Game.InGame.UI.Tutorial
{
    public class VideoTutorialViewManager : MonoBehaviour
    {
        [SerializeField] private RawImage rawImage;
        
        public async UniTask ShowTutorial(string addressable)
        {
            gameObject.SetActive(true);
            
            var tutorialObjectPrefab = await AddressableLoader.LoadAsyncDefault<GameObject>(addressable);
            var tutorialObject = Instantiate(tutorialObjectPrefab);
            
            var controller = tutorialObject.GetComponent<IMovieTutorialController>();
            var renderTexture = new RenderTexture(1280, 720, 24);
            rawImage.texture = renderTexture;
            
            renderTexture.Create();
            await controller.PlayMovie(renderTexture);
            renderTexture.Release();
            
            controller.DestroyTutorial();
            
            gameObject.SetActive(false);
        }
    }
}