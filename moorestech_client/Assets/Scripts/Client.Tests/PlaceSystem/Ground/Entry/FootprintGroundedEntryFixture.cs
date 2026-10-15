using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using Client.Game.InGame.Construction;
using Client.Tests.PlaceSystem.TrainCostIntegration;
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

            // 照準の20.8と占有隅の21.29を別セルにする
            // Put the aim at 20.8 and the footprint corner at 21.29 in different cells
            Ground = Root.transform.Find("Ground").gameObject;
            var rotation = Quaternion.Euler(0, 0, 45);
            Ground.GetComponent<BoxCollider>().size = new Vector3(20, 2, 20);
            Ground.transform.SetPositionAndRotation(new Vector3(501.5f, 20.8f, 600.5f) - rotation * Vector3.up, rotation);

            // 距離判定と照準を実Camera・実プレイヤーで通す
            // Drive reach and aiming with a real camera and player
            Root.transform.Find("Player").position = new Vector3(501.5f, 21, 600.5f);
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

        protected BlockPreviewBoundingBoxSurface CreateFace()
        {
            var face = CreateObject("BlockFace");
            face.layer = LayerConst.BlockBoundingBoxLayer;
            face.transform.position = new Vector3(501.5f, 24.5f, 600.5f);
            face.AddComponent<BoxCollider>().size = new Vector3(10, 1, 10);
            var surface = face.AddComponent<BlockPreviewBoundingBoxSurface>();
            surface.SetPreviewSurfaceType(PreviewSurfaceType.XZ_Y, null);
            Physics.SyncTransforms();
            return surface;
        }
    }
}
