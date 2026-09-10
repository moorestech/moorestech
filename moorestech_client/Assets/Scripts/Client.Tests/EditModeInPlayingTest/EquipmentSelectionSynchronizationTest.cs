using System.Collections;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Equipment;
using Client.Tests.EditModeInPlayingTest.Util;
using Cysharp.Threading.Tasks;
using Game.Context;
using Game.PlayerInventory.Interface;
using NUnit.Framework;
using VContainer;
using UnityEngine.TestTools;

namespace Client.Tests.EditModeInPlayingTest
{
    // shard割当はクラスと一緒に移動・改名される
    // The shard assignment travels with the class through moves and renames
    [Category("CiShardClientPlay3")]
    public class EquipmentSelectionSynchronizationTest
    {
        [UnityTest]
        public IEnumerator クライアントの装備選択送信がサーバーの選択位置へ届く()
        {
            EditModeInPlayingTestUtil.EnterPlayModeUtil();
            yield return new EnterPlayMode(expectDomainReload: true);
            LogAssert.ignoreFailingMessages = true;

            yield return Body().ToCoroutine();
            yield return new ExitPlayMode();

            UnityEditor.SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);

            #region Internal

            async UniTask Body()
            {
                await EditModeInPlayingTestUtil.LoadMainGame();

                // 実クライアント送信のサーバー到達を待つ
                // Wait for the real client request to reach the server
                var equipment = ClientDIContext.DIContainer.DIContainerResolver.Resolve<LocalPlayerEquipment>();
                var playerId = ClientContext.PlayerConnectionSetting.PlayerId;
                var serverEquipment = ServerContext.GetService<IPlayerInventoryDataStore>()
                    .GetInventoryData(playerId).EquipmentInventory;
                var initialConfirmationRevision = equipment.SelectionConfirmationRevision;
                equipment.SetSelectedIndex(1);

                for (var i = 0; i < 100 &&
                                    (serverEquipment.SelectedEquipmentIndex != 1 ||
                                     equipment.SelectionConfirmationRevision == initialConfirmationRevision); i++)
                {
                    await UniTask.Delay(50);
                }

                Assert.AreEqual(1, serverEquipment.SelectedEquipmentIndex);
                Assert.Greater(equipment.SelectionConfirmationRevision, initialConfirmationRevision);
            }

            #endregion
        }
    }
}
