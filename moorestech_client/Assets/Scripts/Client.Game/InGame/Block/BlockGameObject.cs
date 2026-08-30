using System;
using System.Collections.Generic;
using System.Linq;
using Client.Common;
using Client.Game.InGame.Block.Interact;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using Client.Game.InGame.BlockSystem.StateProcessor;
using Client.Game.InGame.Context;
using Client.Game.InGame.Map.NearestSearch;
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
    public class BlockGameObject : MonoBehaviour, INearestSearchTarget
    {
        public BlockId BlockId { get; private set; }
        public BlockInstanceId BlockInstanceId { get; private set; }
        public BlockMasterElement BlockMasterElement { get; private set; }
        public BlockPositionInfo BlockPosInfo { get; private set; }
        public List<IBlockStateChangeProcessor> BlockStateChangeProcessors { get; private set; }
        
        // 最近傍索引の墓標。撤去済みのブロックを木の組み直し無しに候補から外す
        // Tombstone for the nearest index; a removed block leaves the candidates without rebuilding the tree
        public bool IsSearchable { get; private set; } = true;

        public IObservable<BlockGameObject> OnFinishedPlaceAnimation => _onFinishedPlaceAnimation;
        private readonly Subject<BlockGameObject> _onFinishedPlaceAnimation = new();
        
        private RendererShaderAnimation _rendererShaderAnimation;
        private RendererMaterialReplacerController _rendererMaterialReplacerController;
        private List<VisualEffect> _visualEffects = new();
        private List<IPreviewOnlyObject> _previewOnlyObjects = new();

        private BlockStateMessagePack _blockStateMessagePack;
        private bool _isShaderAnimating;
        
        // 索引の構築時に1度だけ読まれる座標。ブロックは動かないので設置位置をそのまま返す
        // The position read once when the index is built; blocks never move, so the placed position is returned as is
        public Vector3 GetIndexPosition()
        {
            return transform.position;
        }

        public void MarkUnsearchable()
        {
            IsSearchable = false;
        }

        public void Initialize(BlockMasterElement blockMasterElement, BlockPositionInfo posInfo, BlockInstanceId blockInstanceId)
        {
            BlockPosInfo = posInfo;
            BlockId = MasterHolder.BlockMaster.GetBlockId(blockMasterElement.BlockGuid);
            BlockInstanceId = blockInstanceId;
            BlockMasterElement = blockMasterElement;
            BlockStateChangeProcessors = gameObject.GetComponentsInChildren<IBlockStateChangeProcessor>().ToList();
            _visualEffects = gameObject.GetComponentsInChildren<VisualEffect>(true).ToList();
            _rendererShaderAnimation = gameObject.AddComponent<RendererShaderAnimation>();
           
            _rendererMaterialReplacerController = new RendererMaterialReplacerController(gameObject);
            
            // 子供のBlockGameObjectChildを初期化（非アクティブな子も後から有効化され得るため対象に含める）
            // Initialize child BlockGameObjectChild components (include inactive ones that may be activated later)
            foreach (var child in gameObject.GetComponentsInChildren<BlockGameObjectChild>(true)) child.Init(this);

            // 開けるブロックのみ面を初期化する
            // Initialize the interact face only when one was attached; attachment itself is decided by BlockGameObjectPrefabContainer
            if (gameObject.TryGetComponent<BlockInteractable>(out var interactable)) interactable.Initialize(this);

            // 地面との衝突判定を無効化
            foreach (var groundCollisionDetector in gameObject.GetComponentsInChildren<GroundCollisionDetector>(true))
            {
                groundCollisionDetector.enabled = false;
            }
            
            // IBlockGameObjectInnerComponentおよび、継承しているBlockStateChangeProcessorsの初期化
            // Initialize IBlockGameObjectInnerComponent and inherited BlockStateChangeProcessors
            foreach (var state in gameObject.GetComponentsInChildren<IBlockGameObjectInnerComponent>()) state.Initialize(this);
            
            // プレビュー限定オブジェクトをオフに
            // Turn off preview-only object
            OffPreviewOnlyObjectsActive();
            
            // ブロックのステート変化を購読
            // Subscribe to block state changes
            SubscribeBlockState();
            
            // バウンディングボックス用オブジェクトを作成
            // Create a bounding box object
            AddBoundingBox().Forget();

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
                            try
                            {
                                processor.OnChangeState(data); 
                            }
                            catch (Exception e)
                            {
                                Debug.LogError($"Name:{BlockMasterElement.Name} Pos: {BlockPosInfo.OriginalPos} BlockStateChangeProcessorの{processor.GetType().Name}で例外が発生しました。\n{e.Message}\n{e.StackTrace}");
                            }
                        }
                        
                        _blockStateMessagePack = data;
                    }).AddTo(this.GetCancellationTokenOnDestroy());
                
                // ブロックの初期状態を取得するためにサーバーに問い合わせる
                // Request the server for the initial block state
                ClientContext.VanillaApi.SendOnly.RequestBlockState(BlockPosInfo.OriginalPos);
            }
            
            async UniTask AddBoundingBox()
            {
                _previewOnlyObjects.Add(await BlockPreviewBoundingBoxLoader.LoadAsync(this, blockMasterElement, posInfo, this.GetCancellationTokenOnDestroy()));
            }

            #endregion
        }
        
        public async UniTask PlayPlaceAnimation()
        {
            _isShaderAnimating = true;
            SetVfxActive(false);
            await _rendererShaderAnimation.PlaceAnimation();
            _isShaderAnimating = false;
            SetVfxActive(true);
            _onFinishedPlaceAnimation.OnNext(this);
        }
        
        public void SetRemovePreviewing()
        {
            if (_isShaderAnimating) return;
            var placePreviewMaterial = MaterialConst.GetPreviewPlaceBlockMaterial();
            
            _rendererMaterialReplacerController.CopyAndSetMaterial(placePreviewMaterial);
            _rendererMaterialReplacerController.SetColor(MaterialConst.PreviewColorPropertyName ,MaterialConst.NotPlaceableColor);
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
            await _rendererShaderAnimation.RemoveAnimation();
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
