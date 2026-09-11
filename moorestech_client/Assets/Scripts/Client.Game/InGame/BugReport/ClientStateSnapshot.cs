using UnityEngine;

namespace Client.Game.InGame.BugReport
{
    // Escape時点のクライアント側の状態。サーバー側には無い情報だけを持つ
    // Client-side state at the Escape moment; holds only what the server does not know
    public sealed class ClientStateSnapshot
    {
        public Vector3 CameraPosition { get; }
        public Vector3 CameraEulerAngles { get; }
        public Vector3 PlayerPosition { get; }
        public string UiState { get; }
        public ulong Tick { get; }

        public ClientStateSnapshot(Vector3 cameraPosition, Vector3 cameraEulerAngles, Vector3 playerPosition, string uiState, ulong tick)
        {
            CameraPosition = cameraPosition;
            CameraEulerAngles = cameraEulerAngles;
            PlayerPosition = playerPosition;
            UiState = uiState;
            Tick = tick;
        }
    }
}
