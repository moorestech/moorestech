using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Client.MovieTutorial.GameObjectMovie
{
    [CreateAssetMenu(fileName = "GamObjectMovieSequence", menuName = "moorestech/GamObjectMovieSequence", order = 0)]
    public class GamObjectMovieSequence : ScriptableObject
    {
        public GameObjectMovieText TextPrefab; 
        
        public List<MovieSequenceInfo> SequenceInfos;
        public List<MovieSequenceTextInfo> SequenceTextInfos;
    }
    
    [Serializable]
    public class MovieSequenceInfo
    {
        public GameObject Prefab;
        public Vector3 Position;
        public Vector3 Rotation;
        public Vector3 Scale = Vector3.one;
        
        public float SpawnTime;
    }
    
    [Serializable]
    public class MovieSequenceTextInfo
    {
        public Vector3 Position;
        public Vector3 Rotation;
        public Vector3 Scale = Vector3.one;
        
        public float SpawnTime;
        public string Text;
    }
}