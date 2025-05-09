using Client.Common.Asset;
using Client.Game.MovieTutorial;
using Client.Game.MovieTutorial.GameObjectMovie;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Game.InGame.UI.Tutorial
{
    public class VideoTutorialViewManager : MonoBehaviour
    {
        [SerializeField] private RawImage rawImage;
        
        public async UniTask ShowTutorial(string addressable, string sequencePath)
        {
            gameObject.SetActive(true);
            
            
            var tutorialObjectPrefab = await AddressableLoader.LoadAsyncDefault<GameObject>(addressable);
            var tutorialObject = Instantiate(tutorialObjectPrefab);
            
            var controller = tutorialObject.GetComponent<IMovieTutorialController>();
            var renderTexture = new RenderTexture(1280, 720, 24);
            rawImage.texture = renderTexture;
            
            var parameter = new GameObjectMovieTutorialParameter(null);
            renderTexture.Create();
            await controller.PlayMovie(parameter, renderTexture);
            renderTexture.Release();
            
            controller.DestroyTutorial();
            
            gameObject.SetActive(false);
        }
    }
}