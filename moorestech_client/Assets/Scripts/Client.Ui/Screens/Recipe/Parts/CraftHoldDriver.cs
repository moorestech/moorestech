using System;
using Client.Ui.Components.Recipe;
using Client.Ui.State.Recipe.Commands;
using Client.Ui.State.Recipe.Logic;
using Client.Ui.State.Toast;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Recipe.Parts
{
    /// <summary>
    /// 長押しクラフトの経過をパネルのスケジューラで進める（Web useHoldCraft の写し。Update は使わない）
    /// Advances the hold-to-craft timer on the panel scheduler (port of the Web useHoldCraft; no Update)
    /// </summary>
    public sealed class CraftHoldDriver
    {
        private const string CraftCommandName = "recipe.craft";

        private readonly VisualElement _scheduleOwner;
        private readonly RecipeRow _row;
        private readonly IRecipeCommands _commands;
        private readonly UiCommandResultToaster _toaster;
        private Guid _recipeGuid;
        private double _craftTime;
        private bool _craftable;
        private double _elapsed;
        private IVisualElementScheduledItem _tick;

        public CraftHoldDriver(VisualElement scheduleOwner, RecipeRow row, IRecipeCommands commands, UiCommandResultToaster toaster)
        {
            _scheduleOwner = scheduleOwner;
            _row = row;
            _commands = commands;
            _toaster = toaster;

            // クラフトの矢印は常に進捗の形（Web は保持中でなければ 0 を渡し、静止表示にしない）
            // A craft arrow always shows progress (the Web passes 0 when not holding, never the static form)
            _row.SetProgress(0f);

            // 木から外れている間はスケジューラが止まり、同じ要素を付け直せば続く（GUID で並べ直すときに Web の key と同じく長押しを保つ。D-34）。捨てるときは要素ごと捨てる
            // While detached the scheduler pauses and resumes when the same element is re-attached (keeps the hold across GUID reordering like the Web key; D-34); discarded entries are dropped whole
        }

        public void SetRecipe(Guid recipeGuid, double craftTime, bool craftable)
        {
            _recipeGuid = recipeGuid;
            _craftTime = craftTime;
            _craftable = craftable;

            // 作れなくなったら止める（無効のボタンは離しを受けられない。Web の useEffect([craftable])）
            // Stop once it can no longer be crafted (a disabled button cannot receive the release; the Web useEffect([craftable]))
            if (!craftable && _tick != null) Stop();
        }

        public void Start()
        {
            // silent-ok: 保持中の二重押下と作れないときの押下は Web と同じく始めない / ボタンは作れないとき無効で、CraftHoldTest・CraftHoldKeyTest が保持と可否を見る
            if (_tick != null || !_craftable) return;
            _elapsed = 0;
            _row.SetProgress(0f);
            _tick = _scheduleOwner.schedule.Execute(Tick).Every(0);
        }

        public void Stop()
        {
            // 経過を捨てて矢印を 0 に戻す（離す・外れる・取消・フォーカス外れ）
            // Drop the elapsed time and reset the arrow (release, leave, cancel, blur)
            _tick?.Pause();
            _tick = null;
            _elapsed = 0;
            _row.SetProgress(0f);
        }

        private void Tick(TimerState timer)
        {
            // 1 フレームの経過は上限で頭打ち（タブ復帰などの大きな飛びで連続クラフトしない）
            // Cap one frame at the limit so a large jump never crafts several times
            var delta = Math.Min(timer.deltaTime / 1000.0, HoldCraftLogic.MaxFrameDeltaSeconds);

            // 保持中に作れなくなったら経過を捨てて待つ（補充されれば続く。Web と同じ）
            // If it becomes uncraftable mid-hold, drop the elapsed time and wait (resumes on refill, as on the Web)
            if (_craftable)
            {
                var step = HoldCraftLogic.Advance(_elapsed, delta, _craftTime);
                _elapsed = step.Elapsed;
                if (step.DidCraft) _toaster.Report(CraftCommandName, _commands.Craft(_recipeGuid));
            }
            else
            {
                _elapsed = 0;
            }
            _row.SetProgress((float)HoldCraftLogic.Progress(_elapsed, _craftTime));
        }
    }
}
