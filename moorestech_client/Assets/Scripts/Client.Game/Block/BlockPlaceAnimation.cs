using System;
using System.Collections.Generic;
using Client.Common;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Client.Game.Block
{
    public class BlockPlaceAnimation : MonoBehaviour
    {
        
        private const string WorldMinY = "_WorldMinY";
        private const string WorldMaxY = "_WorldMaxY";
        private const string DevolveRate = "_DevolveRate";
        private const string LightPower = "_LightPower";
        
        public async UniTask PlayAnimation()
        {
            List<RendererMaterialReplacer> blockRendererInfos = new();
            //元のマテリアルを取得
            // 3Dモデルの最小、最大Y座標を取得する
            var worldMinY = float.MaxValue;
            var worldMaxY = float.MinValue;
            var placeAnimationMaterial  = Resources.Load<Material>(MaterialConst.PlaceBlockAnimationMaterial);
            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                worldMinY = Mathf.Min(worldMinY, renderer.bounds.min.y);
                worldMaxY = Mathf.Max(worldMaxY, renderer.bounds.max.y);
                var rendererInfo = new RendererMaterialReplacer(renderer);
                rendererInfo.SetMaterial(placeAnimationMaterial);
                blockRendererInfos.Add(rendererInfo);
            }

            //マテリアルにセット
            SetMaterialProperty(WorldMinY, worldMinY);
            SetMaterialProperty(WorldMaxY, worldMaxY);
            SetMaterialProperty(DevolveRate, 0);
            SetMaterialProperty(LightPower, 0.6f);
            
            //アニメーションを実行
            TweenMaterialProperty(DevolveRate, 0, 1, 2.5f, Ease.InOutSine);
            TweenMaterialProperty(LightPower, 0.6f, 1, 2.0f, Ease.InOutSine);

            await UniTask.Delay(2000);
            
            SetMaterialProperty(LightPower, 1);
            TweenMaterialProperty(LightPower, 1, 2, 0.4f, Ease.InOutSine);
            await UniTask.Delay(200);
            
            TweenMaterialProperty(LightPower, 2, 0.5f, 0.2f, Ease.InOutSine);
            await UniTask.Delay(200);

            //マテリアルをリセット
            foreach (var rendererInfo in blockRendererInfos)
            {
                rendererInfo.ResetMaterial();
            }
            blockRendererInfos.Clear();

            #region Internal
            
            void TweenMaterialProperty(string keyword,float startValue,float endValue,float duration,Ease ease)
            {
                var value = startValue;
                DOTween.To(() => value, x => value = x, endValue, duration).SetEase(ease).OnUpdate(() =>
                {
                    foreach (var rendererInfo in blockRendererInfos)
                    {
                        rendererInfo.SetPlaceMaterialProperty(keyword, value);
                    }
                });
            }
        
            void SetMaterialProperty(string keyword,float value)
            {
                foreach (var rendererInfo in blockRendererInfos)
                {
                    rendererInfo.SetPlaceMaterialProperty(keyword, value);
                }
            }

            #endregion
        }

    }
}