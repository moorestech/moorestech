using System;
using System.Collections.Generic;
using Core.Master;
using NUnit.Framework;

namespace Tests.UnitTest.Core.Research
{
    public class ResearchMasterTest
    {
        [Test]
        public void ThrowsWhenNodeNotFound()
        {
            // Arrange: empty data
            var json = "{\"data\":[]}";
            var master = new ResearchMaster(json);

            // Act + Assert
            Assert.Throws<KeyNotFoundException>(() => master.GetResearchNode(new Guid("11111111-1111-1111-1111-111111111111")));
        }
    }
}

