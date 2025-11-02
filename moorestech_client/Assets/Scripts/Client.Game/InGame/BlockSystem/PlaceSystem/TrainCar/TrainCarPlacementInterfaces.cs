using Core.Master;
using Server.Protocol.PacketResponse;
using static Server.Protocol.PacketResponse.RailConnectionEditProtocol;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar
{
    public readonly struct TrainCarPlacementHit
    {
        public TrainCarPlacementHit(RailComponentSpecifier specifier, Vector3 previewPosition, Quaternion previewRotation, bool isPlaceable)
        {
            Specifier = specifier;
            PreviewPosition = previewPosition;
            PreviewRotation = previewRotation;
            IsPlaceable = isPlaceable;
        }

        public RailComponentSpecifier Specifier { get; }
        public Vector3 PreviewPosition { get; }
        public Quaternion PreviewRotation { get; }
        public bool IsPlaceable { get; }
    }

    public interface ITrainCarPlacementDetector
    {
        bool TryDetect(ItemId holdingItemId, out TrainCarPlacementHit hit);
    }

    public interface ITrainCarPreviewController
    {
        void Initialize(ItemId itemId);
        void ShowPreview(Vector3 position, Quaternion rotation, bool isPlaceable);
        void HidePreview();
    }

    public interface ITrainCarPlacementInput
    {
        bool IsPlaceTriggered();
    }

    public interface ITrainCarPlacementSender
    {
        void Send(RailComponentSpecifier specifier, int hotBarSlot);
    }
}

