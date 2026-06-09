using Client.Game.InGame.Player.StateController;
using UnityEngine;

namespace Client.Game.InGame.Riding
{
    public interface IRideFollowTargetResolver
    {
        bool TryResolveFollowTarget(RidingPlayerStateContext context, out Transform followTarget);
        bool Exists(RidingPlayerStateContext context);
        Vector3 ResolveDismountPosition(RidingPlayerStateContext context, Vector3 fallbackPosition);
    }
}
