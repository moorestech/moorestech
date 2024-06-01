using System;
using System.Collections.Generic;
using Client.Skit.Skit;
using UnityEngine;

namespace Client.Skit.Define
{
    [CreateAssetMenu(fileName = "CharacterDefine", menuName = "moorestech/CharacterDefine", order = 0)]
    public class CharacterDefine : ScriptableObject
    {
        [SerializeField] private List<CharacterInfo> characterInfos;
        public IReadOnlyList<CharacterInfo> CharacterInfos => characterInfos;
    }
    
    [Serializable]
    public class CharacterInfo
    {
        [SerializeField] private string characterKey;
        [SerializeField] private SkitCharacter characterPrefab;
        public string CharacterKey => characterKey;
        
        public SkitCharacter CharacterPrefab => characterPrefab;
    }
}