// モジュール欄の「1スロット上限 N」が描かれ、上限の解放で書き換わることを撮る
// Capture the module area's "per-slot limit N" label and its rewrite when the limit is raised
// 本番マスタの石窯を使うので、ピン先 master worktree の server_v8 を run-scenario.sh の第3引数で渡す
// Uses the production master's 石窯, so pass the pinned master worktree's server_v8 as run-scenario.sh's third argument
using Client.Game.InGame.UI.UIState;
using Client.Localization;
using Client.Playtest;
using Client.Playtest.Operations;
using Client.Playtest.WebUi;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.Block.Interface.MachineUpgrade;
using UnityEngine;

var ovenBlockName = "石窯";
var flatGroundObjectName = "PlaytestFlatGround";
var testFieldTopY = 200f;
var ovenModelCenterOffset = new Vector3(1.5f, 1f, 1.5f);
var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("module-slot-stack-limit-label", options, async p =>
{
    await p.SkipOpeningSkitIfPlaying();
    await p.Until(() => p.CurrentUiState == UIStateEnum.GameScreen, 15f, "GameScreenに到達");

    // 期待文言は日本語なので、PlayerPrefsに残る選択言語を控えて日本語へ切り替え、最後に戻す
    // The expected labels are Japanese, so note the persisted language, switch to Japanese, and restore it at the end
    var originalLanguageCode = Localize.GetCurrentLanguageCode();
    p.Note($"言語を {originalLanguageCode} から japanese へ切り替える");
    p.Assert(Localize.TrySetChosenLanguage("japanese"), "日本語へ切り替わる");

    // 自然地形の干渉を避けるため、足場を上空へ移して石窯を置く（interact-key-unification と同じ手順）
    // Move the scaffold to empty sky to avoid terrain interference and place the oven (same as interact-key-unification)
    await p.SetupFlatGround();
    GameObject.Find(flatGroundObjectName).transform.position = new Vector3(0f, testFieldTopY - 2f, 0f);
    p.WarpPlayer(new Vector3(0f, testFieldTopY + 1f, 0f));
    await p.WaitSeconds(1.5f);
    var ovenOrigin = new Vector3Int(-1, Mathf.RoundToInt(testFieldTopY), 1);
    p.PlaceBlockDirect(ovenBlockName, ovenOrigin, BlockDirection.North);
    var ovenGameObject = await p.WaitBlockGameObject(ovenOrigin);
    await p.WaitSeconds(1f);

    p.Note("石窯をF単押しで開く");
    await p.AimAt(ovenGameObject.transform.position + ovenModelCenterOffset);
    await p.PressInteract();
    await p.WaitUiState(UIStateEnum.SubInventory, 5f);

    // 初期値（レベル0＝3）が描かれている
    // The initial value (level 0 = 3) is drawn
    var before = await PlaytestDomQuery.Query("machine-module-stack-limit", 2f);
    for (var i = 0; i < 20 && before.Text != "1スロット上限 3"; i++)
    {
        await p.WaitSeconds(0.5f);
        before = await PlaytestDomQuery.Query("machine-module-stack-limit", 2f);
    }
    p.Note($"[Web UI DOM] found={before.Found} text={before.Text}");
    p.Assert(before.Found && before.Text == "1スロット上限 3", "モジュール欄に「1スロット上限 3」が描かれる");
    await p.Screenshot("01-module-stack-limit-3");

    // 上限をレベル1へ上げ、イベント→ミラー→topic→DOM の書き換えを待つ
    // Raise the limit to level 1 and wait for the event → mirror → topic → DOM rewrite
    p.Note("サーバーでスロット上限をレベル1（6）へ上げる");
    p.ServerService<IModuleSlotStackLimitUnlocker>().UnlockLevel(1);
    var after = await PlaytestDomQuery.Query("machine-module-stack-limit", 2f);
    for (var i = 0; i < 20 && after.Text != "1スロット上限 6"; i++)
    {
        await p.WaitSeconds(0.5f);
        after = await PlaytestDomQuery.Query("machine-module-stack-limit", 2f);
    }
    p.Note($"[Web UI DOM] found={after.Found} text={after.Text}");
    p.Assert(after.Found && after.Text == "1スロット上限 6", "解放後に「1スロット上限 6」へ書き換わる");
    await p.Screenshot("02-module-stack-limit-6");

    p.Note($"言語を {originalLanguageCode} へ戻す");
    p.Assert(Localize.TrySetChosenLanguage(originalLanguageCode), "元の言語へ戻る");
});
