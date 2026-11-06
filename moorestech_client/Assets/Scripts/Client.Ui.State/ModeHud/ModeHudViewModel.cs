using System;

namespace Client.Ui.State.ModeHud
{
    /// <summary>
    /// 操作モード HUD の表示状態（判別 union）。Web の App.tsx の uiState 分岐と topic ui.placement_mode の組の写し
    /// Display state of the operation-mode HUD (discriminated union); a copy of Web App.tsx's uiState branch with the ui.placement_mode topic
    /// </summary>
    public abstract class ModeHudViewModel : IEquatable<ModeHudViewModel>
    {
        public static readonly ModeHudViewModel Hidden = new ModeHudHidden();

        public abstract bool Equals(ModeHudViewModel other);
        public override bool Equals(object obj) => Equals(obj as ModeHudViewModel);
        public abstract override int GetHashCode();
    }

    // 何も出さない（GameScreen ほか）
    // Shows nothing (GameScreen and others)
    public sealed class ModeHudHidden : ModeHudViewModel
    {
        internal ModeHudHidden()
        {
        }

        public override bool Equals(ModeHudViewModel other) => other is ModeHudHidden;
        public override int GetHashCode() => 0;
    }

    // 配置モード: 右上の HUD
    // Placement mode: the top-right HUD
    public sealed class ModeHudPlacement : ModeHudViewModel
    {
        public readonly ModeHudText Target;
        public readonly int Height;

        public ModeHudPlacement(ModeHudText target, int height)
        {
            Target = target;
            Height = height;
        }

        public override bool Equals(ModeHudViewModel other) => other is ModeHudPlacement placement && Target.Equals(placement.Target) && Height == placement.Height;
        public override int GetHashCode() => HashCode.Combine(Target, Height);
    }

    // 削除モード: 上下の帯と右上の高さ
    // Delete mode: top and bottom bands with the top-right height
    public sealed class ModeHudDelete : ModeHudViewModel
    {
        public readonly int Height;

        public ModeHudDelete(int height)
        {
            Height = height;
        }

        public override bool Equals(ModeHudViewModel other) => other is ModeHudDelete delete && Height == delete.Height;
        public override int GetHashCode() => Height;
    }
}
