using System;
using Game.PlacementTarget;

namespace Client.Ui.State.BuildMenu.Entries
{
    /// <summary>
    /// 種別の Web の綴り（testid・チュートリアルの錨・プレイテスト DSL が同じ文字列を使う）
    /// The Web spelling of a kind (testids, tutorial anchors and the playtest DSL share these strings)
    /// </summary>
    public static class BuildMenuEntryKindName
    {
        // Web の橋 BuildMenuEntryDtoFactory.ResolveKind と同じ値（BuildMenuEntryFactoryTest が突き合わせる）
        // Same values as the Web bridge BuildMenuEntryDtoFactory.ResolveKind (BuildMenuEntryFactoryTest cross-checks)
        public static string Of(PlacementTargetKind kind)
        {
            return kind switch
            {
                PlacementTargetKind.Block => "block",
                PlacementTargetKind.TrainCar => "trainCar",
                PlacementTargetKind.ConnectTool => "connectTool",
                PlacementTargetKind.BlueprintCopy => "blueprintCopy",
                PlacementTargetKind.Blueprint => "blueprint",
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
            };
        }
    }
}
