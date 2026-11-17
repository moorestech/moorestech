using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Construction;
using Client.Tests.PlaceSystem.TrainCostIntegration;
using Game.Block.Interface;
using Game.Construction;
using UnityEngine;

namespace Client.Tests.PlaceSystem.Ground.Entry
{
    public abstract class FootprintGroundedEntryFixture : TrainPlacementEntryFixture
    {
        protected BlockGameObjectDataStore Blocks;
        protected ConstructionWalletQuery Wallet;
        protected RecordingGroundedPreview Preview;
        protected GameObject Ground;

        public override void Setup()
        {
            base.Setup();
            Blocks = CreateObject("Blocks").AddComponent<BlockGameObjectDataStore>();
            Wallet = new ConstructionWalletQuery(new ClientRemainingPlacementCountDatastore());
            Preview = new RecordingGroundedPreview();

            // 照準20.2と最高点を別セルにする
            // Aim at 20.2 and footprint max in different cells
            Ground = Root.transform.Find("Ground").gameObject;
            var rotation = Quaternion.Euler(0, 0, 45);
            Ground.GetComponent<BoxCollider>().size = new Vector3(20, 2, 20);
            Ground.transform.SetPositionAndRotation(new Vector3(501.5f, 20.2f, 600.5f) - rotation * Vector3.up, rotation);

            // 距離判定と照準を実Camera・実プレイヤーで通す
            // Drive reach and aiming with a real camera and player
            Root.transform.Find("Player").position = new Vector3(501.5f, 21, 600.5f);
            // EditModeのScreenとGameViewの解像度差で中央レイがずれないよう揃える
            // Align the camera viewport with Screen so EditMode and GameView sizes cannot skew the center ray
            PlacementCamera.pixelRect = new Rect(0, 0, Screen.width, Screen.height);
            PlacementCamera.transform.position = new Vector3(501.5f, 40, 600.5f);
            Physics.SyncTransforms();
        }

        protected void RemoveFootprintGround()
        {
            // 照準面は残し、地表探査だけを欠落させる
            // Keep the aim surface while making ground probing fail
            Ground.layer = LayerMask.NameToLayer("Default");
            Physics.SyncTransforms();
        }

        public override void TearDown()
        {
            // 照準の走査対象を設置モード外の状態へ戻す
            // Return the aim traversal target to the outside-placement-mode state
            PlacementAimTargetCells.Disable();
            base.TearDown();
        }

        protected PreviewSurfaceType CreateFace()
        {
            // 上面y=25がカメラ真下で地面20.2より手前に来る、10マス角のブロックを占有セルへ登録する
            // Register a 10-cell block whose top face y=25 sits right below the camera, in front of the ground at 20.2
            Blocks.OccupiedCells.Register(new BlockPositionInfo(new Vector3Int(496, 15, 595), BlockDirection.North, new Vector3Int(10, 10, 10)));
            PlacementAimTargetCells.Enable(Blocks.OccupiedCells);
            return PreviewSurfaceType.XZ_Y;
        }
    }
}
