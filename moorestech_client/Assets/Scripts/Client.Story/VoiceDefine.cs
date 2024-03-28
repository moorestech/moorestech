using System;
using System.Collections.Generic;
using UnityEngine;

namespace Client.Story
{
    [CreateAssetMenu(fileName = "VoiceDefine", menuName = "VoiceDefine", order = 0)]
    public class VoiceDefine : ScriptableObject
    {
        [SerializeField] private List<CharacterVoices> characterVoices;

        public AudioClip GetVoiceClip(string characterKey, string sentence)
        {
            var characterVoice = characterVoices.Find(x => x.CharacterKey == characterKey);
            if (characterVoice == null) return null;

            var voiceInfo = characterVoice.VoiceInfos.Find(x => x.Sentence == sentence);
            return voiceInfo?.VoiceClip;
        }
    }

    [Serializable]
    public class CharacterVoices
    {
        public string CharacterKey => characterKey;
        [SerializeField] private string characterKey;

        public List<VoiceInfo> VoiceInfos => voiceInfos;
        [SerializeField] private List<VoiceInfo> voiceInfos;

        [SerializeField] private string credit;
    }

    [Serializable]
    public class VoiceInfo
    {
        public string Sentence => sentence;
        [SerializeField, Multiline] private string sentence;

        public AudioClip VoiceClip => voiceClip;
        [SerializeField] private AudioClip voiceClip;
    }
}