using System;
using Client.Common;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace Client.Game.InGame.Block
{
    public class BlockShaderAnimation : MonoBehaviour
    {
        private const string WorldMinY = "_WorldMinY";
        private const string WorldMaxY = "_WorldMaxY";
        private const string DevolveRate = "_DevolveRate";
        private const string LightPower = "_LightPower";
        
        private RendererMaterialReplacerController _rendererMaterialReplacerController;
        
        public async UniTask PlaceAnimation()
        {
            _rendererMaterialReplacerController ??= new RendererMaterialReplacerController(gameObject);
            
            //マテリアルを更新
            var placeAnimationMaterial = Resources.Load<Material>(MaterialConst.PlaceBlockAnimationMaterial);
            _rendererMaterialReplacerController.CopyAndSetMaterial(placeAnimationMaterial);
            
            //マテリアルにセット
            var (worldMinY, worldMaxY) = GetWorldMinMaxY();
            SetMaterialProperty(WorldMinY, worldMinY);
            SetMaterialProperty(WorldMaxY, worldMaxY);
            SetMaterialProperty(DevolveRate, 0);
            SetMaterialProperty(LightPower, 0.6f);
            
            //接地されているアニメーション
            TweenMaterialProperty(DevolveRate, 0, 1, 2.5f, Ease.InOutSine);
            TweenMaterialProperty(LightPower, 0.6f, 1, 2.0f, Ease.InOutSine);
            
            await UniTask.Delay(2000);
            
            //最後のフラッシュ
            SetMaterialProperty(LightPower, 1);
            TweenMaterialProperty(LightPower, 1, 2, 0.4f, Ease.InOutSine);
            await UniTask.Delay(200);
            
            TweenMaterialProperty(LightPower, 2, 0.5f, 0.2f, Ease.InOutSine);
            await UniTask.Delay(200);
            
            //マテリアルをリセット
            _rendererMaterialReplacerController.ResetMaterial();
        }
        
        public async UniTask RemoveAnimation()
        {
            _rendererMaterialReplacerController ??= new RendererMaterialReplacerController(gameObject);
            
            //マテリアルを更新
            var placeAnimationMaterial = Resources.Load<Material>(MaterialConst.PlaceBlockAnimationMaterial);
            _rendererMaterialReplacerController.CopyAndSetMaterial(placeAnimationMaterial);
            
            //マテリアルにセット
            var (worldMinY, worldMaxY) = GetWorldMinMaxY();
            SetMaterialProperty(WorldMinY, worldMinY);
            SetMaterialProperty(WorldMaxY, worldMaxY);
            SetMaterialProperty(DevolveRate, 1);
            SetMaterialProperty(LightPower, 0);
            
            //アニメーションを実行
            TweenMaterialProperty(LightPower, 0, 1, 0.5f, Ease.InOutSine);
            
            await UniTask.Delay(250);
            
            TweenMaterialProperty(DevolveRate, 1, 0, 0.55f, Ease.InOutSine);
            
            await UniTask.Delay(500);
        }
        
        private (float worldMinY, float worldMaxY) GetWorldMinMaxY()
        {
            var worldMinY = float.MaxValue;
            var worldMaxY = float.MinValue;
            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                worldMinY = Mathf.Min(worldMinY, renderer.bounds.min.y);
                worldMaxY = Mathf.Max(worldMaxY, renderer.bounds.max.y);
            }
            
            return (worldMinY, worldMaxY);
        }
        
        private void TweenMaterialProperty(string keyword, float startValue, float endValue, float duration, Ease ease)
        {
            var value = startValue;
            DOTween.To(() => value, x => value = x, endValue, duration).SetEase(ease).OnUpdate(() =>
            {
                _rendererMaterialReplacerController.SetPlaceMaterialProperty(keyword, value);
            });
        }
        
        private void SetMaterialProperty(string keyword, float value)
        {
            _rendererMaterialReplacerController.SetPlaceMaterialProperty(keyword, value);
        }
    }
}