using TMPro;
using UnityEngine;

namespace Client.MovieTutorial.GameObjectMovie
{
    public class GameObjectMovieText : MonoBehaviour
    {
        [SerializeField] private TMP_Text textObject;
        
        public void SetText(string text)
        {
            textObject.text = text;
        }
    }
}