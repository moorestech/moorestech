using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.ElectricWireAutoConnect;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Parts;
using Client.Game.InGame.BlockSystem.StateProcessor.ElectricWire;
using Core.Master;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.Util.Blueprint.Planning;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Paste
{
    /// <summary>
    ///     BP内の復元予定配線をプレビューする
    ///     Previews saved connections within a blueprint
    /// </summary>
    public class BlueprintPasteLinePreview
    {
        private readonly Transform _root = new GameObject("BlueprintPasteLines").transform;
        private readonly List<PreviewWireLine> _wires = new();
        private readonly List<BlueprintPasteChainPreview> _chains = new();

        public void Show(BlueprintPastePlan plan, IReadOnlyList<IReadOnlyList<BlockPreviewObject>> ghosts)
        {
            _root.gameObject.SetActive(true);
            var wireCount = 0;
            var chainCount = 0;
            for (var copyIndex = 0; copyIndex < plan.Copies.Count; copyIndex++)
            {
                var copy = plan.Copies[copyIndex];
                foreach (var line in copy.Draft.Lines)
                {
                    // 保存配線の解決済み端点だけをBPの可否色で描く
                    // Draw only resolved saved connections in the copy's judgement color
                    if (line.Kind == BlueprintPasteLineKind.ElectricWire)
                    {
                        if (_wires.Count == wireCount) _wires.Add(new PreviewWireLine(_root));
                        var wire = _wires[wireCount++];
                        wire.SetColor(!copy.IsPlaced);
                        wire.Draw(ResolveWireEndpoint(copyIndex, line.ElementIndexA), ResolveWireEndpoint(copyIndex, line.ElementIndexB));
                    }
                    else
                    {
                        if (_chains.Count == chainCount) _chains.Add(new BlueprintPasteChainPreview(_root));
                        _chains[chainCount++].Draw(GearChainPoleExtendPreviewCalculator.GetPoleCenter(line.PositionA),
                            GearChainPoleExtendPreviewCalculator.GetPoleCenter(line.PositionB), copy.IsPlaced);
                    }
                }

            }

            // 前フレームより減った線はプールへ戻す
            // Hide pooled lines that are no longer in the current plan
            for (var i = wireCount; i < _wires.Count; i++) _wires[i].SetActive(false);
            for (var i = chainCount; i < _chains.Count; i++) _chains[i].SetActive(false);

            #region Internal

            Vector3 ResolveWireEndpoint(int copyIndex, int elementIndex)
            {
                var element = plan.Copies[copyIndex].Draft.Elements[elementIndex];
                var info = new PlaceInfo { Position = element.Position, Direction = element.Direction, BlockId = element.BlockId };
                return ElectricWireEndpointResolver.ResolveFromGhost(ghosts[copyIndex][elementIndex], info, MasterHolder.BlockMaster.GetBlockMaster(element.BlockId));
            }

            #endregion
        }

        public void Hide() => _root.gameObject.SetActive(false);
    }
}
