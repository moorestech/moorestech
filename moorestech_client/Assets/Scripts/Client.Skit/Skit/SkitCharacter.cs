using System;
using System.Collections.Generic;
using Client.Skit.Context;
using CommandForgeGenerator.Command;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace Client.Skit.Skit
{
    public class SkitCharacter : MonoBehaviour
    {
        [SerializeField] private AudioSource voiceAudioSource;
        [SerializeField] private SkinnedMeshRenderer faceSkinnedMeshRenderer;
        [SerializeField] private SkitCharacterAnimator skitCharacterAnimator;
        
        private SkitOrigin _skitOrigin;
        
        public void Initialize(Transform parent)
        {
            skitCharacterAnimator.Initialize();
            gameObject.name += " (StoryCharacter)";
            transform.SetParent(parent);
        }
        
        // スキット開始時に再生文脈の原点を押し込み、加算はこのsinkの中だけで起きるようにする
        // Push the playback context's origin in at skit start so the addition happens only inside this sink
        public void SetSkitOrigin(SkitOrigin skitOrigin)
        {
            _skitOrigin = skitOrigin;
        }
        
        public void SetTransform(SkitRelativePosition position, Vector3 rotation)
        {
            transform.position = position.ToWorld(_skitOrigin);
            transform.eulerAngles = rotation;
        }
        
        public async UniTask PlayAnimation(string animationId, float mixierDuration)
        {
            await skitCharacterAnimator.PlayAnimation(animationId, mixierDuration);
        }
        
        public void PlayVoice(AudioClip voiceClip)
        {
            voiceAudioSource.clip = voiceClip;
            voiceAudioSource.Play();
        }
        
        public void StopVoice()
        {
            voiceAudioSource.Stop();
        }
        
        public void SetEmotion(string emoteBlendShapeName, float duration, float weight)
        {
            var index =faceSkinnedMeshRenderer.sharedMesh.GetBlendShapeIndex(emoteBlendShapeName);
            
            
            DOTween.To(
                () => faceSkinnedMeshRenderer.GetBlendShapeWeight(index),
                x => faceSkinnedMeshRenderer.SetBlendShapeWeight(index, x),
                weight,
                duration);
        }
        
        public (Vector3 pos, Vector3 rot) GetBoneAbsoluteTransform(string boneName)
        {
            var transforms = gameObject.GetComponentsInChildren<Transform>();
            foreach (var transform in transforms)
            {
                if (transform.name == boneName)
                {
                    return (transform.position, transform.eulerAngles);
                }
            }
            
            throw new ArgumentException($"ボーンが見つかりませんでした。指定されたボーン名：{boneName}");
        }
    }
}