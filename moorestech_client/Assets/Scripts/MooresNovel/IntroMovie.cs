using Cysharp.Threading.Tasks;
using MooresNovel;
using UnityEngine;

namespace IntroMovie
{
    public class IntroMovie : MonoBehaviour
    {
        [SerializeField] private VisualNovelManager visualNovelManager;

        private void Start()
        {
            visualNovelManager.SetActive(true);
            visualNovelManager.ExecuteVisualNovel("Intro").Forget();
        }
    }
}