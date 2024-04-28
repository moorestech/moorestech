using System;
using System.Collections.Generic;
using UnityEngine;

namespace Client.Story
{
    [CreateAssetMenu(fileName = "CharacterDefine", menuName = "moorestech/CharacterDefine", order = 0)]
    public class CharacterDefine : ScriptableObject
    {
        public IReadOnlyList<CharacterInfo> CharacterInfos => characterInfos;
        [SerializeField] private List<CharacterInfo> characterInfos;
    }

    [Serializable]
    public class CharacterInfo
    {
        public string CharacterKey => characterKey;
        [SerializeField] private string characterKey;

        public SkitCharacter CharacterPrefab => characterPrefab;
        [SerializeField] private SkitCharacter characterPrefab;
    }
}