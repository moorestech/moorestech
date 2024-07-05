using System.Collections.Generic;
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
        
        public async UniTask PlaceAnimation()
        {
            var blockRenderers = GetBlockRenderers();
            
            //マテリアルを更新
            var placeAnimationMaterial = Resources.Load<Material>(MaterialConst.PlaceBlockAnimationMaterial);
            SetMaterial(blockRenderers, placeAnimationMaterial);
            
            //マテリアルにセット
            var (worldMinY, worldMaxY) = GetWorldMinMaxY();
            SetMaterialProperty(blockRenderers, WorldMinY, worldMinY);
            SetMaterialProperty(blockRenderers, WorldMaxY, worldMaxY);
            SetMaterialProperty(blockRenderers, DevolveRate, 0);
            SetMaterialProperty(blockRenderers, LightPower, 0.6f);
            
            //接地されているアニメーション
            TweenMaterialProperty(blockRenderers, DevolveRate, 0, 1, 2.5f, Ease.InOutSine);
            TweenMaterialProperty(blockRenderers, LightPower, 0.6f, 1, 2.0f, Ease.InOutSine);
            
            await UniTask.Delay(2000);
            
            //最後のフラッシュ
            SetMaterialProperty(blockRenderers, LightPower, 1);
            TweenMaterialProperty(blockRenderers, LightPower, 1, 2, 0.4f, Ease.InOutSine);
            await UniTask.Delay(200);
            
            TweenMaterialProperty(blockRenderers, LightPower, 2, 0.5f, 0.2f, Ease.InOutSine);
            await UniTask.Delay(200);
            
            //マテリアルをリセット
            foreach (var rendererInfo in blockRenderers) rendererInfo.ResetMaterial();
        }
        
        public async UniTask RemoveAnimation()
        {
            var blockRenderers = GetBlockRenderers();
            
            //マテリアルを更新
            var placeAnimationMaterial = Resources.Load<Material>(MaterialConst.PlaceBlockAnimationMaterial);
            SetMaterial(blockRenderers, placeAnimationMaterial);
            
            //マテリアルにセット
            var (worldMinY, worldMaxY) = GetWorldMinMaxY();
            SetMaterialProperty(blockRenderers, WorldMinY, worldMinY);
            SetMaterialProperty(blockRenderers, WorldMaxY, worldMaxY);
            SetMaterialProperty(blockRenderers, DevolveRate, 1);
            SetMaterialProperty(blockRenderers, LightPower, 0);
            
            //アニメーションを実行
            TweenMaterialProperty(blockRenderers, LightPower, 0, 1, 0.5f, Ease.InOutSine);
            
            await UniTask.Delay(250);
            
            TweenMaterialProperty(blockRenderers, DevolveRate, 1, 0, 0.55f, Ease.InOutSine);
            
            await UniTask.Delay(500);
        }
        
        private List<RendererMaterialReplacer> GetBlockRenderers()
        {
            List<RendererMaterialReplacer> blockRenderers = new();
            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                var rendererInfo = new RendererMaterialReplacer(renderer);
                blockRenderers.Add(rendererInfo);
            }
            
            return blockRenderers;
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
        
        private void SetMaterial(List<RendererMaterialReplacer> blockRendererInfos, Material material)
        {
            foreach (var rendererInfo in blockRendererInfos) rendererInfo.CopyAndSetMaterial(material);
        }
        
        private void TweenMaterialProperty(List<RendererMaterialReplacer> blockRendererInfos, string keyword, float startValue, float endValue, float duration, Ease ease)
        {
            var value = startValue;
            DOTween.To(() => value, x => value = x, endValue, duration).SetEase(ease).OnUpdate(() =>
            {
                foreach (var rendererInfo in blockRendererInfos) rendererInfo.SetPlaceMaterialProperty(keyword, value);
            });
        }
        
        private void SetMaterialProperty(List<RendererMaterialReplacer> blockRendererInfos, string keyword, float value)
        {
            foreach (var rendererInfo in blockRendererInfos) rendererInfo.SetPlaceMaterialProperty(keyword, value);
        }
    }
}