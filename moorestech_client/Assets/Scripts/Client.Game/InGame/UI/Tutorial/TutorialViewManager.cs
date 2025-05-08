using Client.Common.Asset;
using Client.MovieTutorial;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Game.InGame.UI.Tutorial
{
    public class TutorialViewManager : MonoBehaviour
    {
        [SerializeField] private RawImage rawImage;
        
        public async UniTask ShowTutorial(string addressable)
        {
            gameObject.SetActive(true);
            
            var tutorialObjectPrefab = await AddressableLoader.LoadAsyncDefault<GameObject>(addressable);
            var tutorialObject = Instantiate(tutorialObjectPrefab);
            
            var controller = tutorialObject.GetComponent<IMovieTutorialController>();
            rawImage.texture = controller.MovieRenderTexture;
            
            await controller.PlayMovie();
            
            gameObject.SetActive(false);
        }
    }
}