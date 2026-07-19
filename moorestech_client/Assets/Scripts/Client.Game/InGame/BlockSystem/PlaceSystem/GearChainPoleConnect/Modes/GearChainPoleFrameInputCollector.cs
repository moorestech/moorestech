using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Input;
using Client.Game.InGame.Control;
using Core.Master;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.Util.Construction;
using UnityEngine;
using static Client.Common.LayerConst;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Modes
{
    /// <summary>
    /// 環境（レイキャスト・入力・ワールド状態・インベントリ）を読み、Decideに渡す入力スナップショットを組み立てる。
    /// 環境読み取りはこのクラスに集約され、以降のパイプラインは値だけで進む。
    /// Reads the environment (raycast, input, world state, inventory) and builds the input snapshot for Decide.
    /// All environment reads are concentrated here so the rest of the pipeline works on values only.
    /// </summary>
    public class GearChainPoleFrameInputCollector
    {
        // 通常ブロック設置と同等の設置可能距離
        // Placeable distance equivalent to common block placement
        private const float PlaceableMaxDistance = 100f;

        private readonly Camera _mainCamera;
        private readonly ILocalPlayerInventory _playerInventory;
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;
        private readonly GearChainPoleExtendPreviewObject _previewObject;

        public GearChainPoleFrameInputCollector(Camera mainCamera, ILocalPlayerInventory playerInventory, BlockGameObjectDataStore blockGameObjectDataStore, GearChainPoleExtendPreviewObject previewObject)
        {
            _mainCamera = mainCamera;
            _playerInventory = playerInventory;
            _blockGameObjectDataStore = blockGameObjectDataStore;
            _previewObject = previewObject;
        }

        public GearChainPolePlaceExtendInput CollectPlaceExtend(IGearChainPoleConnectAreaCollider sourcePole, BlockMasterElement poleBlockMaster, bool isAwaitingResponse)
        {
            var poleParam = (GearChainPoleBlockParam)poleBlockMaster.BlockParam;

            // 選択中ポールのBlockIdと建設コストをマスタから解決する
            // Resolve the selected pole's BlockId and construction cost from master
            var poleBlockId = MasterHolder.BlockMaster.GetBlockId(poleBlockMaster.BlockGuid);
            var reservedItemCounts = ConstructionCostService.ToItemCounts(poleBlockMaster.RequiredItems);

            var input = new GearChainPolePlaceExtendInput
            {
                HitPole = GetHitPole(),
                SourcePole = sourcePole,
                Clicked = IsScreenClicked(),
                IsAwaitingResponse = isAwaitingResponse,
                PoleBlockId = poleBlockId,
                OwnedChainItemId = GearChainPoleItemFinder.FindOwnedChainItemId(_playerInventory),
                MaxConnectionCount = poleParam.MaxConnectionCount,
            };
            if (sourcePole != null)
            {
                input.SourcePolePos = sourcePole.GetBlockPosition();
                input.SourcePoleCenter = GearChainPoleExtendPreviewCalculator.GetPoleCenter(input.SourcePolePos);
            }

            // ポール命中中は起点選択操作になるためゴースト候補は集めない
            // While hitting a pole the operation is source selection, so no ghost candidate is collected
            if (input.HitPole != null) return input;

            // ゴースト位置を算出し距離内か確認
            // Calculate ghost position and ensure it is within placeable distance
            if (!PlaceSystemUtil.TryGetRayHitBlockPosition(_mainCamera, 0, BlockDirection.North, poleBlockMaster, out var placePos, out _)) return input;
            if (PlaceableMaxDistance < Vector3.Distance(_mainCamera.transform.position, placePos)) return input;

            input.HasGhost = true;
            // BlockId未設定だとプレビュー生成がBlockElement not foundで毎フレーム死ぬ（セル毎BlockId化への追従漏れ）
            // Without BlockId the preview creation dies every frame with BlockElement not found (missed per-cell BlockId migration)
            input.GhostPlaceInfo = new PlaceInfo { Position = placePos, Direction = BlockDirection.North, VerticalDirection = BlockVerticalDirection.Horizontal, Placeable = true, BlockId = poleBlockId };
            input.GhostGroundClear = _previewObject.PositionGhost(input.GhostPlaceInfo, poleBlockMaster);
            input.GhostCenter = GearChainPoleExtendPreviewCalculator.GetPoleCenter(placePos);

            // 起点があれば延長の設置可否を評価しておく
            // Pre-evaluate extension placeability when a source exists
            if (sourcePole != null) input.ExtendPreview = GearChainPoleExtendPreviewCalculator.CalculateExtend(input.SourcePolePos, placePos, poleParam, reservedItemCounts, _blockGameObjectDataStore, _playerInventory, input.OwnedChainItemId);

            return input;
        }

        public GearChainPoleChainConnectInput CollectChainConnect(IGearChainPoleConnectAreaCollider sourcePole)
        {
            // 接続に使うチェーンアイテムをインベントリから自動選択する（手持ち非依存）
            // Auto-select the chain item from inventory, independent of the held item
            var ownedChainItemId = GearChainPoleItemFinder.FindOwnedChainItemId(_playerInventory);

            var input = new GearChainPoleChainConnectInput
            {
                HitPole = GetHitPole(),
                SourcePole = sourcePole,
                Clicked = IsScreenClicked(),
                HoldingChainItemId = ownedChainItemId,
            };
            if (sourcePole != null)
            {
                input.SourcePolePos = sourcePole.GetBlockPosition();
                input.SourcePoleCenter = GearChainPoleExtendPreviewCalculator.GetPoleCenter(input.SourcePolePos);
            }

            // ポール命中時は起点↔対象の接続を評価、非命中時はカーソル位置を赤線用に集める
            // Evaluate source-to-target connection on pole hit, or collect the cursor point for the red line otherwise
            if (input.HitPole != null)
            {
                input.HitPolePos = input.HitPole.GetBlockPosition();
                if (sourcePole != null && input.SourcePolePos != input.HitPolePos) input.PoleToPolePreview = GearChainPoleExtendPreviewCalculator.CalculatePoleToPole(input.SourcePolePos, input.HitPolePos, _blockGameObjectDataStore, _playerInventory, ownedChainItemId);
            }
            else if (sourcePole != null && PlaceSystemUtil.TryGetRayHitPosition(_mainCamera, out var cursorPoint, out _))
            {
                input.HasCursorPoint = true;
                input.CursorPoint = cursorPoint;
            }

            return input;
        }

        private bool IsScreenClicked()
        {
            // UI上のクリックはブロック設置操作として扱わない
            // Ignore clicks over UI as placement input
            return InputManager.Playable.ScreenLeftClick.GetKeyDown && !UiPointerHitTest.IsPointerOverAnyUi();
        }

        private IGearChainPoleConnectAreaCollider GetHitPole()
        {
            PlaceSystemUtil.TryGetRaySpecifiedComponentHit<IGearChainPoleConnectAreaCollider>(
                _mainCamera,
                out var collider,
                Without_Player_MapObject_BlockBoundingBox_LayerMask);
            return collider;
        }
    }
}
