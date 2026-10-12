using Server.Protocol.PacketResponse;
using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal;
using Core.Master;
using Game.Train.SaveLoad;
using UnityEngine;

namespace Client.Tests.BuildUndo
{
    /// <summary>
    ///     復元送信を順に記録するテスト用送信器
    ///     Test sender recording restore sends in order
    /// </summary>
    public class FakeRemovalRestoreSender : IRemovalRestoreSender
    {
        public readonly List<string> Sent = new();
        public readonly List<List<PlaceInfo>> PlacedBatches = new();

        public void PlaceBlocks(List<PlaceInfo> placeInfos)
        {
            PlacedBatches.Add(new List<PlaceInfo>(placeInfos));
            Sent.Add($"place:{placeInfos.Count}");
        }

        public void ConnectElectricWire(Vector3Int posA, Vector3Int posB, Guid connectToolGuid)
        {
            Sent.Add($"wire:{posA}-{posB}:{connectToolGuid}");
        }

        public void ConnectGearChain(Vector3Int posA, Vector3Int posB, Guid connectToolGuid)
        {
            Sent.Add($"chain:{posA}-{posB}:{connectToolGuid}");
        }

        public void ConnectRail(ConnectionDestination from, ConnectionDestination to, Guid connectToolGuid)
        {
            Sent.Add($"rail:{(Vector3Int)from.blockPosition}-{(Vector3Int)to.blockPosition}:{connectToolGuid}");
        }

        public void NotifyRestoreSkipped(int skippedCount)
        {
            Sent.Add($"skipped:{skippedCount}");
        }
    }
}
