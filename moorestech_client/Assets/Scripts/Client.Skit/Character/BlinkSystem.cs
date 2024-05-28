using System;
using DG.Tweening;
using UnityEngine;

namespace Client.Skit.Character
{
    public class BlinkSystem : MonoBehaviour
    {
        [SerializeField] private SkinnedMeshRenderer faceSkinnedMeshRenderer;
        [SerializeField] private string blinkBlendShapeName = "Blink";

        private int _blinkBlendShapeIndex;

        private float _blinkTimer;
        private bool _isBlinking;

        private void Start()
        {
            _blinkBlendShapeIndex = faceSkinnedMeshRenderer.sharedMesh.GetBlendShapeIndex(blinkBlendShapeName);
        }

        private void Update()
        {
            _blinkTimer -= Time.deltaTime;
            if (_blinkTimer <= 0)
            {
                _blinkTimer = UnityEngine.Random.Range(2f, 4f);

                // DoTweenでウェイトを変更する
                DOTween.To(
                    () => faceSkinnedMeshRenderer.GetBlendShapeWeight(_blinkBlendShapeIndex),
                    x => faceSkinnedMeshRenderer.SetBlendShapeWeight(_blinkBlendShapeIndex, x),
                    100,
                    0.1f).OnComplete(() =>
                {
                    DOTween.To(
                        () => faceSkinnedMeshRenderer.GetBlendShapeWeight(_blinkBlendShapeIndex),
                        x => faceSkinnedMeshRenderer.SetBlendShapeWeight(_blinkBlendShapeIndex, x),
                        0,
                        0.1f);
                });
            }
        }
    }
}