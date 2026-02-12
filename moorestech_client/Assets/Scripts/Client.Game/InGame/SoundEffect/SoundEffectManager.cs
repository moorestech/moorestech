using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.SoundEffect
{
    /// <summary>
    ///     TODO 仮のSE専用マネージャ 将来的な作り変えを意識しつつ、とりあえずこれで実装する
    /// </summary>
    public class SoundEffectManager : MonoBehaviour
    {
        [SerializeField] private AudioClip destroyBlockSound;
        [SerializeField] private AudioClip destroyStoneSound;
        [SerializeField] private AudioClip destroyTreeSound;
        [SerializeField] private AudioClip destroyBushSound;
        [SerializeField] private AudioClip placeBlockSound;
        
        [SerializeField] private AudioSource audioSource;
        
        private readonly Dictionary<SoundEffectType, AudioClip> _soundEffectTypeToAudioClip = new();
        
        /// <summary>
        ///     サウンド関係はstaticの方がべんりかな、、って思うけど、改善したほうがいいような気もする
        /// </summary>
        public static SoundEffectManager Instance { get; private set; }
        
        private void Awake()
        {
            Instance = this;
            return;
            
            // 一旦サウンド基盤を作り直すので、すべてのサウンドが出ないようにする
            _soundEffectTypeToAudioClip.Add(SoundEffectType.DestroyBlock, destroyBlockSound);
            _soundEffectTypeToAudioClip.Add(SoundEffectType.DestroyStone, destroyStoneSound);
            _soundEffectTypeToAudioClip.Add(SoundEffectType.DestroyTree, destroyTreeSound);
            _soundEffectTypeToAudioClip.Add(SoundEffectType.DestroyBush, destroyBushSound);
            _soundEffectTypeToAudioClip.Add(SoundEffectType.PlaceBlock, placeBlockSound);
            
        }
        
        public void PlaySoundEffect(SoundEffectType soundEffectType)
        {
            audioSource.PlayOneShot(_soundEffectTypeToAudioClip[soundEffectType]);
        }
    }
    
    public enum SoundEffectType
    {
        DestroyBlock,
        DestroyStone,
        DestroyTree,
        DestroyBush,
        PlaceBlock,
    }
}