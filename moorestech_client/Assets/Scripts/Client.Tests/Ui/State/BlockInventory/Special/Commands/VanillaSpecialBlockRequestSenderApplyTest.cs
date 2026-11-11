using System;
using System.Collections.Generic;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Ui.State.BlockDetail;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.BlockInventory.Special.Commands;
using Client.Ui.State.Items;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Ui.State.BlockInventory.Special
{
    /// <summary>
    /// 分配器の応答は、応答が着いた時点で送った個体が開いているときだけ集約キャッシュへ当てる（待機後の再判定）
    /// A splitter response is applied to the aggregate cache only if the block it was sent for is open when the response arrives (re-checked after the await)
    /// </summary>
    public class VanillaSpecialBlockRequestSenderApplyTest
    {
        private static readonly BlockInstanceId Sent = new(77);
        private ReactiveProperty<BlockInventoryPanelState> _open;
        private BlockNetworkInfoCache _cache;
        private VanillaSpecialBlockRequestSender _sender;
        private FilterSplitterStateProtocol.FilterSplitterStateResponse _response;

        [SetUp]
        public void SetUp()
        {
            _open = new ReactiveProperty<BlockInventoryPanelState>(Panel(Sent));
            _cache = new BlockNetworkInfoCache();
            _sender = new VanillaSpecialBlockRequestSender(_cache, new FakePanelSource(_open));
            _response = new FilterSplitterStateProtocol.FilterSplitterStateResponse(true, FilterSplitterStateProtocol.FilterSplitterStateFailureReason.None, 0, 0, new List<FilterSplitterStateProtocol.DirectionStatePack>());
        }

        [Test]
        public void 応答が着いた時に別のブロックへ切り替わっていたら当てない()
        {
            _open.Value = Panel(new BlockInstanceId(78));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("分配器の応答を捨てました"));
            var outcome = _sender.ApplyFilterResponse(_response, Sent);
            Assert.AreEqual(SpecialBlockRequestOutcomeKind.Accepted, outcome.Kind);
            Assert.IsNull(_cache.FilterSplitter);
        }

        [Test]
        public void 閉じて同じブロックを開き直した後に応答が着いたら当てる()
        {
            _open.Value = BlockInventoryPanelState.Closed;
            _open.Value = Panel(Sent);
            _sender.ApplyFilterResponse(_response, Sent);
            Assert.AreSame(_response, _cache.FilterSplitter);
        }

        [Test]
        public void 閉じたままなら当てない()
        {
            _open.Value = BlockInventoryPanelState.Closed;
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("分配器の応答を捨てました"));
            _sender.ApplyFilterResponse(_response, Sent);
            Assert.IsNull(_cache.FilterSplitter);
        }

        private static OpenBlockInventoryPanel Panel(BlockInstanceId id)
        {
            return new OpenBlockInventoryPanel(BlockMasterElement.BlockTypeConst.FilterSplitter, Guid.Empty, default, id, new Vector3Int(3, 0, 4), Array.Empty<SlotItem>(), BlockInventoryPanelError.None);
        }
    }
}
