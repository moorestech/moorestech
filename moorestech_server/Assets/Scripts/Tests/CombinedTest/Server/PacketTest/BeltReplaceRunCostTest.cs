using System;
using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface;
using Game.Context;
using NUnit.Framework;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.CombinedTest.Server.PacketTest.PlaceBlockProtocolTestSupport;

namespace Tests.CombinedTest.Server.PacketTest
{
    /// <summary>
    /// 複数セルを1通で張り替えたときのコスト精算を検証する。クライアントのプレビュー（BeltReplaceCostSimulator）が同値を主張する基準値でもある
    /// Verifies the cost settlement when one packet replaces several cells; these are also the reference values the client preview (BeltReplaceCostSimulator) claims to match
    /// </summary>
    public class BeltReplaceRunCostTest
    {
        [Test]
        public void 所持素材0財布0の6セル張替えはセル逐次評価で1セルも成立しない()
        {
            var (packet, serviceProvider) = CreateServer();
            UnlockBlock(serviceProvider, ForUnitTestModBlockId.LargeGearBeltConveyor);
            var positions = CreateGearBeltLine(new Vector3Int(80, 0, 80), 6);

            packet.GetPacketResponse(CreateReplaceRunPayload(ForUnitTestModBlockId.LargeGearBeltConveyor, positions), new PacketResponseContext(null));

            // 先頭セルが払えず撤去も財布操作もされないので、以降のセルも同じ状態のまま拒否され続ける
            // The first cell cannot pay and is left untouched wallet and all, so every later cell hits the very same state and is rejected too
            foreach (var position in positions) Assert.AreEqual(ForUnitTestModBlockId.GearBeltConveyor, ServerContext.WorldBlockDatastore.GetBlock(position).BlockId);
        }

        [Test]
        public void 所持素材1セットあれば6セル張替えが全て成立し返却分が1セット残る()
        {
            var (packet, serviceProvider) = CreateServer();
            UnlockBlock(serviceProvider, ForUnitTestModBlockId.LargeGearBeltConveyor);
            GrantRequiredItems(serviceProvider, ForUnitTestModBlockId.LargeGearBeltConveyor, 1);
            var positions = CreateGearBeltLine(new Vector3Int(84, 0, 84), 6);

            packet.GetPacketResponse(CreateReplaceRunPayload(ForUnitTestModBlockId.LargeGearBeltConveyor, positions), new PacketResponseContext(null));

            // 1セット払って新財布が満ち、3セル目・6セル目の撤去で1セットずつ戻るので最後まで払い続けられる
            // One paid set fills the new wallet, and the third and sixth removals each hand a set back, so every cell stays payable
            foreach (var position in positions) Assert.AreEqual(ForUnitTestModBlockId.LargeGearBeltConveyor, ServerContext.WorldBlockDatastore.GetBlock(position).BlockId);
            AssertRequiredItemsCount(serviceProvider, ForUnitTestModBlockId.LargeGearBeltConveyor, 1);
        }

        // 財布も課金元も通さずに既設ラインを敷く（新品ワールドで張替え対象だけがある状態を作る）
        // Lays the existing line without touching the wallet or the payer store, leaving a fresh world holding only the replace targets
        private static List<Vector3Int> CreateGearBeltLine(Vector3Int origin, int length)
        {
            var positions = new List<Vector3Int>();
            for (var i = 0; i < length; i++)
            {
                var position = origin + new Vector3Int(0, 0, i);
                ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearBeltConveyor, position, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
                positions.Add(position);
            }
            return positions;
        }

        private static byte[] CreateReplaceRunPayload(BlockId blockId, IReadOnlyList<Vector3Int> positions)
        {
            var placeInfos = new List<PlaceInfo>();
            foreach (var position in positions)
            {
                placeInfos.Add(new PlaceInfo
                {
                    Position = position,
                    Direction = BlockDirection.North,
                    VerticalDirection = BlockVerticalDirection.Horizontal,
                    BlockId = blockId,
                    IsReplace = true,
                });
            }
            return CreatePlacePayload(placeInfos);
        }
    }
}
