using System;
using System.Collections.Generic;
using Client.Game.InGame.Map.NearestSearch;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Map.NearestSearch
{
    /// <summary>
    ///     guid別索引の分離と上書き再構築を検証
    ///     Verifies per-guid separation and overwrite-rebuild of the index
    /// </summary>
    public class NearestTargetIndexTest
    {
        private static readonly Guid TreeGuid = new("00000000-0000-0000-0000-000000000001");
        private static readonly Guid RockGuid = new("00000000-0000-0000-0000-000000000002");

        [Test]
        public void 別guidの近い個体は返さない()
        {
            var index = new NearestTargetIndex<NearestSearchTestTarget>();
            var farTree = new NearestSearchTestTarget(new Vector3(100f, 0f, 0f));
            var nearRock = new NearestSearchTestTarget(new Vector3(1f, 0f, 0f));
            index.SetTargets(TreeGuid, new[] { farTree });
            index.SetTargets(RockGuid, new[] { nearRock });

            Assert.AreSame(farTree, index.SearchNearest(TreeGuid, Vector3.zero));
            Assert.AreSame(nearRock, index.SearchNearest(RockGuid, Vector3.zero));
        }

        [Test]
        public void 未登録guidと空リストはnullを返す()
        {
            var index = new NearestTargetIndex<NearestSearchTestTarget>();
            Assert.IsNull(index.SearchNearest(TreeGuid, Vector3.zero));

            index.SetTargets(TreeGuid, new List<NearestSearchTestTarget>());
            Assert.IsNull(index.SearchNearest(TreeGuid, Vector3.zero));
        }

        [Test]
        public void 同じguidへのSetTargetsは前の点集合を置き換える()
        {
            var index = new NearestTargetIndex<NearestSearchTestTarget>();
            var first = new NearestSearchTestTarget(new Vector3(1f, 0f, 0f));
            var second = new NearestSearchTestTarget(new Vector3(50f, 0f, 0f));
            index.SetTargets(TreeGuid, new[] { first, second });
            Assert.AreSame(first, index.SearchNearest(TreeGuid, Vector3.zero));

            index.SetTargets(TreeGuid, new[] { second });
            Assert.AreSame(second, index.SearchNearest(TreeGuid, Vector3.zero));
        }
    }
}
