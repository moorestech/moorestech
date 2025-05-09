using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Client.Game.MovieTutorial.GameObjectMovie.Objects
{
    public class GameObjectMovieText : MonoBehaviour , IGameObjectMovieObject
    {
        [SerializeField] private TMP_Text textObject;
        
        public void SetParameters(List<string> parameters)
        {
            textObject.text = parameters[0];
        }
    }
}