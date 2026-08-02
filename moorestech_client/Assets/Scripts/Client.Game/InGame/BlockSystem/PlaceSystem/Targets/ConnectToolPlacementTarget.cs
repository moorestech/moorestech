using System;
using Core.Master;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Targets
{
    public sealed class ConnectToolPlacementTarget : IPlacementTarget
    {
        // 選択されたconnectToolのGuid。種別・アイコン・素材はマスタから解決する
        // Guid of the selected connectTool; type, icon and materials are resolved from the master
        public readonly Guid ConnectToolGuid;

        public Guid Id => ConnectToolGuid;
        public PlacementTargetKind Kind => PlacementTargetKind.ConnectTool;
        public string DisplayName => MasterHolder.ConnectToolMaster.GetElementOrNull(ConnectToolGuid).Name;

        public ConnectToolPlacementTarget(Guid connectToolGuid)
        {
            ConnectToolGuid = connectToolGuid;
        }

        public bool Equals(IPlacementTarget other)
        {
            return other is ConnectToolPlacementTarget target && ConnectToolGuid == target.ConnectToolGuid;
        }

        public override bool Equals(object obj) => obj is IPlacementTarget target && Equals(target);
        public override int GetHashCode() => ConnectToolGuid.GetHashCode();
    }
}
