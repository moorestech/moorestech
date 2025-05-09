using System;
using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.MovieTutorial.GameObjectMovie
{
    [CreateAssetMenu(fileName = "GamObjectMovieSequence", menuName = "moorestech/GamObjectMovieSequence", order = 0)]
    public class GamObjectMovieSequence : ScriptableObject
    {
        public List<MovieSequenceInfo> SequenceInfos;
    }
    
    [Serializable]
    public class MovieSequenceInfo
    {
        public GameObject Prefab;
        public Vector3 Position;
        public Vector3 Rotation;
        public Vector3 Scale = Vector3.one;
        
        public float SpawnTime;
        
        // TODo このvaluesは変えたい
        public List<string> Parameters;
    }
}