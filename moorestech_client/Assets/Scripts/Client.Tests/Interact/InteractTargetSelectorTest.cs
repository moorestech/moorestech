using System.Collections.Generic;
using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.Control.ViewMode;
using Client.Game.InGame.Interact;
using Client.Game.InGame.Interact.Selection;
using Client.Input;
using Client.Tests.Common;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Interact
{
    /// <summary>
    ///     照準優先・ゴースト貫通・近傍フォールバック・選定可否の4規則を検証（ADR 0046）
    ///     Verifies the four selection rules of ADR 0046: aim first, ghost pass-through, nearby fallback, availability gate
    /// </summary>
    public class InteractTargetSelectorTest : InteractTargetSelectorTestFixture
    {
        [Test]
        public void 照準レイのヒットが2m以内なら選ばれ2mを超えると選ばれない()
        {
            AimPointProvider.SetViewMode(PlayerViewMode.FirstPerson);
            var target = CreateMapObjectTarget(AimRay().GetPoint(1f));
            PlayerObject.transform.position = target.transform.position;

            var selector = new InteractTargetSelector();
            Assert.AreSame(target, selector.Select());

            // 2mを超えると照準ヒットでも候補にならず、近傍にも無いのでnull
            // Beyond 2m the aim hit is discarded and nothing is nearby, so null
            PlayerObject.transform.position = target.transform.position + new Vector3(0f, 0f, InteractTargetSelector.InteractDistance + 0.5f);
            Assert.IsNull(selector.Select());
        }

        [Test]
        public void 手前の設置ゴーストは貫通してその奥の対象が選ばれる()
        {
            AimPointProvider.SetViewMode(PlayerViewMode.FirstPerson);
            var target = CreateMapObjectTarget(AimRay().GetPoint(1f));

            // 設置ゴーストは常に照準の手前に出るため、貫通しないと奥の対象を永久に掴めない
            // The placement ghost always sits in front of the aim, so without pass-through the target behind is unreachable
            var ghostObject = new GameObject("BlockPreview");
            ghostObject.transform.position = AimRay().GetPoint(0.5f);
            ghostObject.AddComponent<BlockPreviewObject>();
            TargetObjects.Add(ghostObject);

            var ghostCollider = new GameObject("BlockPreviewMesh") { layer = LayerConst.BlockLayer };
            ghostCollider.transform.SetParent(ghostObject.transform, false);
            ghostCollider.AddComponent<SphereCollider>().radius = 0.05f;

            PlayerObject.transform.position = target.transform.position;
            Physics.SyncTransforms();

            Assert.AreSame(target, new InteractTargetSelector().Select());
        }

        [Test]
        public void 照準に何も無ければ半径2m内で視線角度が最小の候補が選ばれる()
        {
            AimPointProvider.SetViewMode(PlayerViewMode.FirstPerson);
            CameraObject.transform.position = new Vector3(0f, 1f, -5f);
            CameraObject.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            PlayerObject.transform.position = Vector3.zero;

            // 前方1.5m/角度0、右1.0m/角度90
            // One 1.5m ahead (angle 0) and a closer one 1.0m to the right (angle 90)
            var ahead = CreateMapObjectTarget(new Vector3(0f, 0f, 1.5f));
            CreateMapObjectTarget(new Vector3(1.0f, 0f, 0f));

            Assert.AreSame(ahead, new InteractTargetSelector().Select());
        }

        [Test]
        public void マスタ未解決のmapObjectは照準に当たっても選ばれない()
        {
            AimPointProvider.SetViewMode(PlayerViewMode.FirstPerson);
            var target = CreateMapObjectTarget(AimRay().GetPoint(1f));
            PlayerObject.transform.position = target.transform.position;

            // マスタ未解決なら対象にならない
            // Selection passes through IsInteractAvailable, so a master-less object under the aim is no target
            TestReflection.SetField(target, "<MapObjectMasterElement>k__BackingField", null);
            Assert.IsNull(new InteractTargetSelector().Select());
        }

        [Test]
        public void 列車は当たり判定子からでも車両のインタラクト面が選ばれる()
        {
            AimPointProvider.SetViewMode(PlayerViewMode.FirstPerson);
            var interactable = CreateTrainCarTarget(AimRay().GetPoint(1f));
            PlayerObject.transform.position = interactable.transform.position;
            Physics.SyncTransforms();

            Assert.AreSame(interactable, new InteractTargetSelector().Select());
        }

        [Test]
        public void 照準ヒットが2mより遠いときは近傍候補が選ばれる()
        {
            AimPointProvider.SetViewMode(PlayerViewMode.FirstPerson);
            CameraObject.transform.position = Vector3.zero;
            CameraObject.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            PlayerObject.transform.position = Vector3.zero;

            // 5m先の照準ヒットは手が届かず、横1mの候補が残る
            // The aim hit 5m away is out of reach, leaving the candidate 1m to the side
            CreateMapObjectTarget(AimRay().GetPoint(5f));
            var nearby = CreateMapObjectTarget(new Vector3(1f, 0f, 0f));

            Assert.AreSame(nearby, new InteractTargetSelector().Select());
        }

        [Test]
        public void Select前の問い合わせは走査結果が無いので空で返る()
        {
            var selector = new InteractTargetSelector();
            var keys = new List<InputKey> { InputManager.Playable.Interact };

            selector.CollectCandidateKeys(keys);

            Assert.IsEmpty(keys);
            Assert.IsNull(selector.SelectRespondingTo(InputManager.Playable.Ride));
        }

        [Test]
        public void 近傍候補のキーは重複なく集まりキーに応じる候補が返る()
        {
            AimPointProvider.SetViewMode(PlayerViewMode.FirstPerson);
            CameraObject.transform.position = new Vector3(0f, 1f, -5f);
            CameraObject.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            PlayerObject.transform.position = Vector3.zero;

            // 正面の開けるブロック（F）と、右の車両（F・E）
            // An openable block ahead (F) and a train car to the right (F and E)
            var block = CreateOpenableBlockTarget(new Vector3(0f, 0f, 1.5f));
            var trainCar = CreateTrainCarTarget(new Vector3(1f, 0f, 0f));

            var selector = new InteractTargetSelector();
            Assert.AreSame(block, selector.Select());

            var keys = new List<InputKey>();
            selector.CollectCandidateKeys(keys);
            CollectionAssert.AreEquivalent(new[] { InputManager.Playable.Interact, InputManager.Playable.Ride }, keys);
            Assert.AreSame(trainCar, selector.SelectRespondingTo(InputManager.Playable.Ride));
        }

        [Test]
        public void 開けるブロックは当たり判定子からでもインタラクト面が選ばれる()
        {
            AimPointProvider.SetViewMode(PlayerViewMode.FirstPerson);
            var interactable = CreateOpenableBlockTarget(AimRay().GetPoint(1f));
            PlayerObject.transform.position = interactable.transform.position;
            Physics.SyncTransforms();

            Assert.AreSame(interactable, new InteractTargetSelector().Select());
        }

        [Test]
        public void 開けないブロックは照準に当たっても選ばれない()
        {
            AimPointProvider.SetViewMode(PlayerViewMode.FirstPerson);

            // 面が無いブロックは解決先が無い
            // A block with no interact face attached, such as a belt conveyor, resolves to nothing
            var blockObject = new GameObject("PlainBlock");
            blockObject.transform.position = AimRay().GetPoint(1f);
            var blockGameObject = blockObject.AddComponent<BlockGameObject>();
            TargetObjects.Add(blockObject);

            var meshChild = new GameObject("BlockMesh") { layer = LayerConst.BlockLayer };
            meshChild.transform.SetParent(blockObject.transform, false);
            meshChild.AddComponent<SphereCollider>().radius = 0.05f;
            meshChild.AddComponent<BlockGameObjectChild>().Init(blockGameObject);

            PlayerObject.transform.position = blockObject.transform.position;
            Physics.SyncTransforms();

            Assert.IsNull(new InteractTargetSelector().Select());
        }
    }
}
