using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint;
using Client.Tests.BeltSegment.Network;
using Game.Block.Interface;
using Game.Blueprint;
using NUnit.Framework;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.PlaceSystem.Blueprint
{
    public sealed class BlueprintPasteDirectionDiagnosticTest
    {
        [Test]
        public void EachExplicitPasteAttemptReportsInvalidDirectionsOnly()
        {
            BeltNetworkFixture.LoadMaster();
            var id = ForUnitTestModBlockId.GearBeltConveyor;
            var invalid = new BlueprintPlacementElement(new Vector3Int(8,0,8), BlockDirection.UpNorth, id, null);
            var valid = new BlueprintPlacementElement(new Vector3Int(9,0,8), BlockDirection.North, id, null);
            int count = 0;
            Application.logMessageReceived += Count;
            try
            {
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    LogAssert.Expect(LogType.Warning, $"[BlueprintPaste] Rejected belt direction: blockId={id}, direction={invalid.Direction}, position={invalid.Position}");
                    BlueprintPasteSystem.ReportRejectedDirections(new[] { invalid, valid });
                }
                BlueprintPasteSystem.ReportRejectedDirections(new[] { valid });
                BlueprintPasteSystem.ReportRejectedDirections(System.Array.Empty<BlueprintPlacementElement>());
                Assert.AreEqual(2, count);
            }
            finally { Application.logMessageReceived -= Count; }
            #region Internal
            void Count(string message, string stack, LogType type)
            {
                if (message.StartsWith("[BlueprintPaste] Rejected belt direction:")) count++;
            }
            #endregion
        }
    }
}
