using System.Collections.Generic;
using Client.Game.Skit;
using CommandForgeGenerator.Command;
using NUnit.Framework;

namespace Client.Tests.Skit
{
    public class SkitWorldObjectControlGroupTest
    {
        [Test]
        public void SetActiveReachesEveryRegisteredWorldObject()
        {
            var mapObjects = new RecordingWorldObjectControl();
            var outcrops = new RecordingWorldObjectControl();
            var group = new SkitWorldObjectControlGroup(
                new List<ISkitWorldObjectControl> { mapObjects, outcrops });

            group.SetActive(false);
            group.SetActive(true);

            CollectionAssert.AreEqual(new[] { false, true }, mapObjects.ReceivedValues);
            CollectionAssert.AreEqual(new[] { false, true }, outcrops.ReceivedValues);
        }

        private sealed class RecordingWorldObjectControl : ISkitWorldObjectControl
        {
            public readonly List<bool> ReceivedValues = new();

            public void SetActive(bool enable)
            {
                ReceivedValues.Add(enable);
            }
        }
    }
}
