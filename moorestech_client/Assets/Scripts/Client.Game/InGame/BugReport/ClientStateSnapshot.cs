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

        // 取れなかった位置は原点で埋まる。実値と区別できないと調査側が原点に居たと読んでしまう
        // An unavailable position falls back to the origin; without these flags an investigator reads it as really being there
        public bool HasCamera { get; }
        public bool HasPlayer { get; }

        public ClientStateSnapshot(Vector3 cameraPosition, Vector3 cameraEulerAngles, Vector3 playerPosition, string uiState, ulong tick, bool hasCamera, bool hasPlayer)
        {
            CameraPosition = cameraPosition;
            CameraEulerAngles = cameraEulerAngles;
            PlayerPosition = playerPosition;
            UiState = uiState;
            Tick = tick;
            HasCamera = hasCamera;
            HasPlayer = hasPlayer;
        }
    }
}
