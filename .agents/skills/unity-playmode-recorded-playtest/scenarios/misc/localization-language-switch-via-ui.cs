// Guid辞書の言語切替E2E
// GUID-dictionary locale-switch E2E
using Client.Game.InGame.UI.UIState;
using Client.Localization;
using Client.Playtest;
using Client.Playtest.Operations;
using Cysharp.Threading.Tasks;
using Mooresmaster.Localization.Generated;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

// 輸送カテゴリに車両サブカテゴリがあり、車両名の実バグ（同一addressablePathで"Locomotive"が2件）の回帰点になる
// The transport category holds the train-car sub category, the regression point for the duplicate "Locomotive" bug
var transportCategoryTestId = "build-menu-category-d1000000-0000-4000-8000-000000000007";
var miningCategoryTestId = "build-menu-category-d1000000-0000-4000-8000-000000000001";
var steamLocomotiveTestId = "build-menu-entry-trainCar-9e3215d5-175e-4600-adee-2c32f786d124";
var dieselLocomotiveTestId = "build-menu-entry-trainCar-019f0d20-d52e-7172-8b55-bd9b79b6feb1";
var windDrillGuid = Guid.Parse("934c0ef9-b76e-4058-8fc8-0ad74afbdcd0");
var steamLocomotiveGuid = Guid.Parse("9e3215d5-175e-4600-adee-2c32f786d124");
var dieselLocomotiveGuid = Guid.Parse("019f0d20-d52e-7172-8b55-bd9b79b6feb1");
var windDrillEntryTestId = PlaytestWebUiOps.BuildMenuBlockTestId("風力掘削機");

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("localization-language-switch-via-ui", options, async p =>
{
    p.Note("デバッグ環境を整え、全ブロックを解放した状態で言語切替を検証する");
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());

    // 開幕スキット(Story)中はポーズメニューもビルドメニューも開けないためSkipインテントで抜ける
    // The opening skit (Story) blocks both the pause menu and the build menu, so skip it via the intent path
    await p.SkipOpeningSkit();

    // ロケールはPlayerPrefsへ永続化されるため、前回ランの残留を潰してから日本語で始める
    // The locale persists in PlayerPrefs, so clear any carry-over from the previous run and start in Japanese
    p.Note("ポーズメニューを開き、開始ロケールを日本語へ揃える");
    await SwitchLocale("japanese");
    p.Assert(Localize.GetContent(ContentLocalizationKeys.BlockName(windDrillGuid)) == "風力掘削機",
        "日本語ロケールでブロック名が日本語原文へ解決される");

    // 詳細サイドバー表示のためホバーする
    // Hover to capture it in the detail sidebar
    p.Note("日本語のまま2種の機関車をホバーし、別名で表示されることを確認する");
    await OpenBuildMenu();
    await p.ClickWebUi(transportCategoryTestId);
    await p.HoverWebUi(steamLocomotiveTestId);
    await PlaytestWebUiOps.WaitWebUiTextContains("build-menu-detail", "蒸気機関車", 15f);
    await p.Screenshot("01-traincar-steam-japanese");
    await p.HoverWebUi(dieselLocomotiveTestId);
    await PlaytestWebUiOps.WaitWebUiTextContains("build-menu-detail", "ディーゼル機関車", 15f);
    await p.Screenshot("02-traincar-diesel-japanese");

    // 車両名はマスタのnameが正で、addressablePath末尾ではないことを辞書経路で固定する
    // Pin that train-car names come from the master name, not the addressablePath tail
    p.Assert(Localize.GetContent(ContentLocalizationKeys.TrainCarName(steamLocomotiveGuid)) == "蒸気機関車",
        "蒸気機関車がマスタnameから解決される");
    p.Assert(Localize.GetContent(ContentLocalizationKeys.TrainCarName(dieselLocomotiveGuid)) == "ディーゼル機関車",
        "ディーゼル機関車が同一addressablePathでも別名で解決される");

    p.Note("日本語のままブロック名と配置HUDを確認する");
    await p.ClickWebUi(miningCategoryTestId);
    await p.HoverWebUi(windDrillEntryTestId);
    await PlaytestWebUiOps.WaitWebUiTextContains("build-menu-detail", "風力掘削機", 15f);
    await p.Screenshot("03-block-preview-japanese");
    await p.ClickBuildMenuBlock("風力掘削機");
    await p.WaitUiState(UIStateEnum.PlaceBlock, 15f);
    await PlaytestWebUiOps.WaitWebUiTextContains("placement-mode-hud", "風力掘削機", 15f);
    await p.Screenshot("04-placement-hud-japanese");

    // 英語へ切替え、辞書revisionのpushだけでWebの表示名が張り替わることを確認する
    // Switch to English and confirm the web display names re-render from the dictionary revision push alone
    p.Note("Englishへ切り替える");
    await p.ExitToGameScreen();
    await SwitchLocale("english");
    p.Assert(Localize.GetContent(ContentLocalizationKeys.BlockName(windDrillGuid)) == "Wind Drill",
        "Englishロケールでブロック名が英訳へ解決される");

    p.Note("英語でブロック名と配置HUDが英語化されることを確認する");
    await OpenBuildMenu();
    await p.ClickWebUi(miningCategoryTestId);
    await p.HoverWebUi(windDrillEntryTestId);
    await PlaytestWebUiOps.WaitWebUiTextContains("build-menu-detail", "Wind Drill", 15f);
    await p.Screenshot("05-block-preview-english");
    await p.ClickBuildMenuBlock("風力掘削機");
    await p.WaitUiState(UIStateEnum.PlaceBlock, 15f);
    await PlaytestWebUiOps.WaitWebUiTextContains("placement-mode-hud", "Wind Drill", 15f);
    await p.Screenshot("06-placement-hud-english");

    // 車両はmod辞書に訳が無いため、English時も原文へフォールバックするのが期待動作
    // Train cars have no mod translations, so falling back to the source text under English is expected
    p.Note("未翻訳の車両名はEnglishでも原文へフォールバックする");
    await p.ExitToGameScreen();
    await OpenBuildMenu();
    await p.ClickWebUi(transportCategoryTestId);
    await p.HoverWebUi(steamLocomotiveTestId);
    await p.Screenshot("07-traincar-steam-english");
    p.Assert(Localize.GetContent(ContentLocalizationKeys.TrainCarName(steamLocomotiveGuid)) == "蒸気機関車",
        "未翻訳の車両名はEnglishでも原文へフォールバックする");

    p.Note("日本語へ戻して往復を閉じる");
    await p.PressKey(Key.B);
    await p.WaitUiState(UIStateEnum.GameScreen, 15f);
    await SwitchLocale("japanese");
    p.Assert(Localize.GetContent(ContentLocalizationKeys.BlockName(windDrillGuid)) == "風力掘削機",
        "日本語へ戻すとブロック名も日本語原文へ戻る");
    await p.Screenshot("08-back-to-japanese");

    #region Internal

    async UniTask SwitchLocale(string locale)
    {
        await p.PressKey(Key.Escape);
        await p.WaitUiState(UIStateEnum.PauseMenu, 15f);
        await p.UntilWebUiElement("pause-menu", 15f);
        await p.ClickWebUi($"language-select-option-{locale}");
        await p.UntilWebUiElement($"pause-menu-locale-{locale}", 15f);
        await p.PressKey(Key.Escape);
        await p.WaitUiState(UIStateEnum.GameScreen, 15f);
    }

    async UniTask OpenBuildMenu()
    {
        await p.PressKey(Key.B);
        await p.UntilWebUiElement("build-menu-panel", 15f);
    }

    #endregion
});
