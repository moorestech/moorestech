using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Thumbnail;
using NUnit.Framework;

namespace Client.Tests.PlaceSystem
{
    public class BlueprintThumbnailSyncPlannerTest
    {
        [Test]
        public void 未撮影だけ撮り_消えたBPだけ捨てる()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var gone = Guid.NewGuid();
            var plan = BlueprintThumbnailSyncPlanner.Plan(new List<Guid> { a, b }, new List<Guid> { a, gone });
            CollectionAssert.AreEqual(new[] { b }, plan.ToRender);
            CollectionAssert.AreEqual(new[] { gone }, plan.ToRemove);
        }

        [Test]
        public void 空ライブラリは全て捨てる_空キャッシュは全て撮る()
        {
            var a = Guid.NewGuid();
            var emptyLibrary = BlueprintThumbnailSyncPlanner.Plan(new List<Guid>(), new List<Guid> { a });
            Assert.AreEqual(0, emptyLibrary.ToRender.Count);
            Assert.AreEqual(1, emptyLibrary.ToRemove.Count);

            var emptyCache = BlueprintThumbnailSyncPlanner.Plan(new List<Guid> { a }, new List<Guid>());
            Assert.AreEqual(1, emptyCache.ToRender.Count);
            Assert.AreEqual(0, emptyCache.ToRemove.Count);
        }
    }
}
