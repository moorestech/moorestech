using UnityEngine;

namespace Server.Protocol.PacketResponse.Util.Blueprint.Planning
{
    public readonly struct BlueprintPasteOrigin
    {
        public readonly Vector3Int Position;
        public readonly bool IsGroundFound;

        public BlueprintPasteOrigin(Vector3Int position, bool isGroundFound)
        {
            Position = position;
            IsGroundFound = isGroundFound;
        }
    }
}
