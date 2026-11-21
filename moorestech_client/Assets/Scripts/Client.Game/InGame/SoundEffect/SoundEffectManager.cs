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
        private readonly SoundEffectRepeatLimiter _repeatLimiter = new();
        
        /// <summary>
        ///     サウンド関係はstaticの方がべんりかな、、って思うけど、改善したほうがいいような気もする
        /// </summary>
        public static SoundEffectManager Instance { get; private set; }
        
        private void Awake()
        {
            _soundEffectTypeToAudioClip.Add(SoundEffectType.DestroyBlock, destroyBlockSound);
            _soundEffectTypeToAudioClip.Add(SoundEffectType.DestroyStone, destroyStoneSound);
            _soundEffectTypeToAudioClip.Add(SoundEffectType.DestroyTree, destroyTreeSound);
            _soundEffectTypeToAudioClip.Add(SoundEffectType.DestroyBush, destroyBushSound);
            _soundEffectTypeToAudioClip.Add(SoundEffectType.PlaceBlock, placeBlockSound);
            
            Instance = this;
        }
        
        public void PlaySoundEffect(SoundEffectType soundEffectType)
        {
            // 同じ音が重なって割れないよう、短い間隔の連打は間引く
            // Thin out rapid repeats so the same sound does not stack and clip
            if (!_repeatLimiter.TryAccept(soundEffectType, Time.unscaledTime)) return;
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