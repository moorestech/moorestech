using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.UI.Tooltip;
using Client.WebUiHost.Common;
using Client.WebUiHost.Game.Topics;
using Core.Master;
using Game.PlacementTarget;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.WebUi
{
    public class BlueprintPlacementModeContractTest
    {
        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void PlacementModeFactorySeparatesTypedCopyToolFromRawBlueprintName()
        {
            var copyTool = PlacementModeDtoFactory.Create(
                new BlueprintCopyPlacementTarget(
                    MasterHolder.BuildToolMaster.All[0].BuildToolGuid),
                2,
                "",
                wheelOwnedByTool: false);
            var blueprint = PlacementModeDtoFactory.Create(
                new BlueprintPlacementTarget(
                    Guid.Parse("60000000-0000-4000-8000-000000000001"),
                    "My Blueprint", new global::Game.Blueprint.BlueprintJsonObject()),
                2,
                "",
                wheelOwnedByTool: false);

            // BPコピーはtyped、命名BPはraw
            // Type blueprint copies while preserving authored blueprint names
            Assert.AreEqual("blueprintCopy", copyTool.SelectedTargetType);
            Assert.IsNull(copyTool.SelectedName);
            Assert.AreEqual("raw", blueprint.SelectedTargetType);
            Assert.AreEqual("My Blueprint", blueprint.SelectedName);
        }

    }
}
