using System;
using System.Collections.Generic;
using UnityEngine;

namespace Client.Skit.Define
{
    [CreateAssetMenu(fileName = "VoiceDefine", menuName = "moorestech/VoiceDefine", order = 0)]
    public class VoiceDefine : ScriptableObject
    {
        [SerializeField] private List<CharacterVoices> characterVoices;
        
        public AudioClip GetVoiceClip(string characterKey, string sentence)
        {
            var characterVoice = characterVoices.Find(x => x.CharacterKey == characterKey);
            if (characterVoice == null) return null;
            
            var voiceInfo = characterVoice.VoiceInfos.Find(x => sentence.Contains(x.Sentence));
            return voiceInfo?.VoiceClip;
        }
    }
    
    [Serializable]
    public class CharacterVoices
    {
        [SerializeField] private string characterKey;
        [SerializeField] private List<VoiceInfo> voiceInfos;
        
        [SerializeField] private string credit;
        public string CharacterKey => characterKey;
        
        public List<VoiceInfo> VoiceInfos => voiceInfos;
    }
    
    [Serializable]
    public class VoiceInfo
    {
        [SerializeField] [Multiline] private string sentence;
        [SerializeField] private AudioClip voiceClip;
        public string Sentence => sentence;
        
        public AudioClip VoiceClip => voiceClip;
    }
}