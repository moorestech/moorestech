using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.State.Text;
using System.Collections.Generic;
using Client.Game.InGame.GraphicsQuality;
using Client.Ui.Components.Controls;
using Client.Ui.State.Settings;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Settings.GraphicsQualityRows
{
    /// <summary>
    /// 画質の 1 行。値の型は行の中に閉じ、ビューは型を知らずに並べて今の設定値を流すだけにする
    /// One graphics-quality row; its value type stays inside the row, so the view only lists rows and pushes the current settings
    /// </summary>
    public interface IGraphicsQualityRow
    {
        LocalizationKey Caption { get; }
        IGraphicsQualityRowSwitch CreateSwitch(ISettingsCommands commands, IUiOsCursor osCursor);
    }

    public interface IGraphicsQualityRowSwitch
    {
        VisualElement Element { get; }
        void Apply(GraphicsQualitySettings settings);
    }

    /// <summary>
    /// 型付きの行の共通部。設定値からの読み出しと 1 項目の差し替えを派生が型で持つ
    /// Shared part of a typed row; derived rows own the typed read from and single-item replace on the settings
    /// </summary>
    public abstract class GraphicsQualityRow<TValue> : IGraphicsQualityRow
    {
        public LocalizationKey Caption { get; }
        public string Testid { get; }
        public IReadOnlyList<ModeSwitchOption<TValue>> Options { get; }

        protected GraphicsQualityRow(LocalizationKey caption, string testid, IReadOnlyList<ModeSwitchOption<TValue>> options)
        {
            Caption = caption;
            Testid = testid;
            Options = options;
        }

        public abstract TValue Read(GraphicsQualitySettings settings);
        public abstract GraphicsQualitySettings With(GraphicsQualitySettings settings, TValue value);

        public IGraphicsQualityRowSwitch CreateSwitch(ISettingsCommands commands, IUiOsCursor osCursor)
        {
            return new GraphicsQualityRowSwitch<TValue>(this, new ModeSwitch<TValue>(Testid, ModeSwitchOrientation.Horizontal, ModeSwitchRole.Group, Options, osCursor), commands);

        }
    }

    internal sealed class GraphicsQualityRowSwitch<TValue> : IGraphicsQualityRowSwitch
    {
        private readonly GraphicsQualityRow<TValue> _row;
        private readonly ModeSwitch<TValue> _switch;
        private GraphicsQualitySettings _current;

        public VisualElement Element => _switch;

        public GraphicsQualityRowSwitch(GraphicsQualityRow<TValue> row, ModeSwitch<TValue> modeSwitch, ISettingsCommands commands)
        {
            _row = row;
            _switch = modeSwitch;

            // 項目の変更は今の設定値の 1 項目だけを差し替えて渡す
            // An item change hands over the current settings with only that item replaced
            _switch.OnOptionSelected.Subscribe(value => commands.SetGraphicsQuality(_row.With(_current, value)));
        }

        public void Apply(GraphicsQualitySettings settings)
        {
            _current = settings;
            _switch.SetSelected(_row.Read(settings));
        }
    }
}
