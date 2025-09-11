using System;
using System.Collections.Generic;
using System.Linq;
using Client.Common;
using Client.Common.Asset;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.StateProcessor;
using Client.Game.InGame.Context;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using MessagePack;
using Mooresmaster.Model.BlocksModule;
using Server.Event.EventReceive;
using UniRx;
using UnityEngine;
using UnityEngine.VFX;

namespace Client.Game.InGame.Block
{
    public class BlockGameObject : MonoBehaviour
    {
        public BlockId BlockId { get; private set; }
        public BlockMasterElement BlockMasterElement { get; private set; }
        public BlockPositionInfo BlockPosInfo { get; private set; }
        public List<IBlockStateChangeProcessor> BlockStateChangeProcessors { get; private set; }
        
        public IObservable<BlockGameObject> OnFinishedPlaceAnimation => _onFinishedPlaceAnimation;
        private readonly Subject<BlockGameObject> _onFinishedPlaceAnimation = new();
        
        private BlockShaderAnimation _blockShaderAnimation;
        private RendererMaterialReplacerController _rendererMaterialReplacerController;
        private List<VisualEffect> _visualEffects = new();
        private List<IPreviewOnlyObject> _previewOnlyObjects = new();
        private const string PreviewBoundingBoxAddressablePath = "Vanilla/Block/Util/BlockPreviewBoundingBox";
        
        private BlockStateMessagePack _blockStateMessagePack;
        private bool _isShaderAnimating;
        
        public void Initialize(BlockMasterElement blockMasterElement, BlockPositionInfo posInfo)
        {
            BlockPosInfo = posInfo;
            BlockId = MasterHolder.BlockMaster.GetBlockId(blockMasterElement.BlockGuid);
            BlockMasterElement = blockMasterElement;
            BlockStateChangeProcessors = gameObject.GetComponentsInChildren<IBlockStateChangeProcessor>().ToList();
            _visualEffects = gameObject.GetComponentsInChildren<VisualEffect>(true).ToList();
            _blockShaderAnimation = gameObject.AddComponent<BlockShaderAnimation>();
           
            _rendererMaterialReplacerController = new RendererMaterialReplacerController(gameObject);
            
            // 子供のBlockGameObjectChildを初期化
            foreach (var child in gameObject.GetComponentsInChildren<BlockGameObjectChild>()) child.Init(this);
            
            // 地面との衝突判定を無効化
            foreach (var groundCollisionDetector in gameObject.GetComponentsInChildren<GroundCollisionDetector>(true))
            {
                groundCollisionDetector.enabled = false;
            }
            
            // プレビュー限定オブジェクトをオフに
            // Turn off preview-only object
            OffPreviewOnlyObjectsActive();
            
            // ブロックのステート変化を購読
            // Subscribe to block state changes
            SubscribeBlockState();
            
            // バウンディングボックス用オブジェクトを作成
            // Create a bounding box object
            LoadBoundingBox().Forget();
            
            #region Internal
            
            void OffPreviewOnlyObjectsActive()
            {
                _previewOnlyObjects = gameObject.GetComponentsInChildren<IPreviewOnlyObject>(true).ToList();
                _previewOnlyObjects.ForEach(obj =>
                {
                    obj.Initialize(BlockId);
                    obj.SetActive(false);
                });
            }
            
            void SubscribeBlockState()
            {
                var eventTag = ChangeBlockStateEventPacket.CreateSpecifiedBlockEventTag(posInfo);
                ClientContext.VanillaApi.Event.SubscribeEventResponse(eventTag,
                    payload =>
                    {
                        var data = MessagePackSerializer.Deserialize<BlockStateMessagePack>(payload);
                        if (data.Position != BlockPosInfo.OriginalPos) return;
                        
                        foreach (var processor in BlockStateChangeProcessors)
                        {
                            processor.OnChangeState(data);
                        }
                        
                        _blockStateMessagePack = data;
                    }).AddTo(this.GetCancellationTokenOnDestroy());
                
                // ブロックの初期状態を取得するためにサーバーに問い合わせる
                // Request the server for the initial block state
                ClientContext.VanillaApi.SendOnly.InvokeBlockState(BlockPosInfo.OriginalPos);
            }
            
            async UniTask LoadBoundingBox()
            {
                var previewBoundingBoxPrefab = await AddressableLoader.LoadAsyncDefault<GameObject>(PreviewBoundingBoxAddressablePath);
                var previewBoundingBoxObj = Instantiate(previewBoundingBoxPrefab, transform);
                previewBoundingBoxObj.GetComponent<BlockPreviewBoundingBox>().SetBoundingBox(blockMasterElement.BlockSize, posInfo.BlockDirection);
                
                var previewOnlyObject = previewBoundingBoxObj.GetComponent<PreviewOnlyObject>();
                previewOnlyObject.Initialize(BlockId);
                previewOnlyObject.SetActive(false);
                _previewOnlyObjects.Add(previewOnlyObject);
            }
            
            #endregion
        }
        
        public async UniTask PlayPlaceAnimation()
        {
            _isShaderAnimating = true;
            SetVfxActive(false);
            await _blockShaderAnimation.PlaceAnimation();
            _isShaderAnimating = false;
            SetVfxActive(true);
            _onFinishedPlaceAnimation.OnNext(this);
        }
        
        public void SetRemovePreviewing()
        {
            if (_isShaderAnimating) return;
            var placePreviewMaterial = Resources.Load<Material>(MaterialConst.PreviewPlaceBlockMaterial);
            
            _rendererMaterialReplacerController.CopyAndSetMaterial(placePreviewMaterial);
            _rendererMaterialReplacerController.SetColor(MaterialConst.PreviewColorPropertyName ,MaterialConst.NotPlaceableColor);
            Resources.UnloadAsset(placePreviewMaterial);
        }
        
        public void ResetMaterial()
        {
            if (_isShaderAnimating) return;
            _rendererMaterialReplacerController.ResetMaterial();
        }
        
        public void EnablePreviewOnlyObjects(bool active, bool renderEnable)
        {
            _previewOnlyObjects.ForEach(obj =>
            {
                obj.SetActive(active);
                obj.SetEnableRenderers(renderEnable);
            });
        }
        
        public async UniTask DestroyBlock()
        {
            _isShaderAnimating = true;
            SetVfxActive(false);
            await _blockShaderAnimation.RemoveAnimation();
            Destroy(gameObject);
        }
        
        public TBlockState GetStateDetail<TBlockState>(string stateKey)
        {
            return _blockStateMessagePack == null ? default : _blockStateMessagePack.GetStateDetail<TBlockState>(stateKey);
        } 
        
        private void SetVfxActive(bool isActive)
        {
            foreach (var vfx in _visualEffects) vfx.gameObject.SetActive(isActive);
        }
    }
}