using System.Collections.Generic;
using Client.Ui.Components.Controls;
using Client.Ui.Screens.BlockInventory.Special.Registration;
using Client.Ui.State.BlockInventory.Logic.Special;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.Items;
using Client.Ui.State.Text;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Special.Space
{
    /// <summary>
    /// 発射台の題の操作。合図待ちの段階だけ建造/発射のボタンを 1 つ出す（Web RocketLaunchPadCommandAction）
    /// Launch pad title action; shows the single build or launch button only while waiting for a command (Web RocketLaunchPadCommandAction)
    /// </summary>
    public sealed class RocketLaunchPadTitleActionView : VisualElement, ISpecialSectionRenderer<SpecialBlockDetailViewModel>
    {
        public const string CommandName = "rocket_launch_pad.command";

        private readonly SpecialViewKit _kit;
        private readonly Dictionary<RocketCommand, PanelActionButton> _buttons = new();

        public RocketLaunchPadTitleActionView(SpecialViewKit kit)
        {
            _kit = kit;
            name = "rocket-launch-pad-title-action";
        }

        public void Render(IReadOnlyList<SlotItem> itemSlots, SpecialBlockDetailViewModel detail)
        {
            // 合図待ちでなければ題の操作ごと隠す（Web は null。手動の操作を持たない段階）。根は alpha の題の操作の器の唯一の子で UiGapContainer の項目ではないので自分で隠してよい（区画とは違う）
            // Hide the whole action outside command waits (Web returns null); the root is the only child of alpha's title-action holder, not a UiGapContainer item, so it may hide itself (unlike sections)
            var hasButton = detail.TryGetRocketLaunchPad(out var rocket) && rocket.TryGetCommandButton(out var button) ? Show(button) : HideAll();
            SpecialViewParts.SetShown(this, hasButton);

            #region Internal

            bool Show(RocketCommandButton shown)
            {
                if (!_buttons.ContainsKey(shown.Command)) _buttons[shown.Command] = CreateButton(shown);
                foreach (var pair in _buttons) SpecialViewParts.SetShown(pair.Value, pair.Key == shown.Command);
                return true;
            }

            bool HideAll()
            {
                foreach (var pair in _buttons) SpecialViewParts.SetShown(pair.Value, false);
                return false;
            }

            PanelActionButton CreateButton(RocketCommandButton source)
            {
                var created = new PanelActionButton($"rocket-launch-pad-{source.Command.ToString().ToLowerInvariant()}", new UiText(source.LabelKey));
                created.OnClicked.Subscribe(_ => _kit.Run(CommandName, _kit.Commands.SendRocketCommand(source.Command)));
                Add(created);
                return created;
            }

            #endregion
        }
    }
}
