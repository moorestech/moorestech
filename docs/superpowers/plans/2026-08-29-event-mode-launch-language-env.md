# 出展モード起動言語の環境変数指定 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** 出展モード（`MOORESTECH_EVENT_MODE=1`）の起動言語を環境変数 `MOORESTECH_EVENT_LANGUAGE` で指定できるようにし、gamescom起動スクリプトでドイツ語を指定する。

**Architecture:** 既存の `EventExhibitionSettings.Parse`（環境変数の純粋パーサ）に言語コード列を1つ足し、`EventModeAutoStart` が固定の `Localize.DefaultLanguageCode` の代わりに `settings.LanguageCode` を適用する。未知値の検証は `Localize.GetLanguageCodes()`（生成テーブル）に対して行い english へ落とす。

**Tech Stack:** Unity C# (Client.Starter / Client.Localization), NUnit EditMode, bash

## Requirements

- `EventExhibitionSettings` に `LanguageCode` を追加し、環境変数 `MOORESTECH_EVENT_LANGUAGE` から読む。受け入れ: `Parse` 単体テストで unset/空 → `english`、既知値 → その値、未知値 → `english`
- 未知値のときは `Debug.LogError` を1回出す（起動は止めない）。受け入れ: `FromEnvironment` 内で raw と確定値が違えば LogError
- `EventModeAutoStart.AutoStartIfEventMode` は `settings.LanguageCode` を `Localize.TrySetLanguage` に渡す。受け入れ: `DefaultLanguageCode` 直参照が消える
- `scripts/event/start-gamescom-loop.command` に `export MOORESTECH_EVENT_LANGUAGE=german` を追加。受け入れ: `bash -n` が通り、grep で行が存在
- 既存テスト `EventExhibitionModeTest` が引数追加後も全て通る
- やらないこと: ADR 0040 の言語選択ゲート、C# 既定値の変更（english のまま）、配布ビルドの作成

## Global Constraints

- `Parse` は純粋関数のまま（Unity API 非依存）。Unity 依存（LogError・`Localize`）は `FromEnvironment` / `AutoStart` 側に置く
- デフォルト引数禁止、partial 禁止、try-catch 禁止（AGENTS.md）
- コメントは日本語→英語の2行セット
- 1ファイル200行以内

---

### Task 1: EventExhibitionSettings に LanguageCode を追加

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/EventMode/EventExhibitionSettings.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/EventMode/EventModeAutoStart.cs:32`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/EventMode/EventExhibitionModeTest.cs`

**Interfaces:**
- Produces: `EventExhibitionSettings.Parse(string enableRawValue, string idleTimeoutRawValue, string editorOptInRawValue, bool isEditor, string languageRawValue, IReadOnlyCollection<string> supportedLanguageCodes)`、`public readonly string LanguageCode`

- [ ] **Step 1: 失敗するテストを書く**（`EventExhibitionModeTest.cs`。既存4テストの `Parse` 呼び出しにも末尾 `, null, Codes` を足す）

```csharp
private static readonly string[] Codes = { "english", "japanese", "german" };

[Test]
public void Parse_LanguageCode_FallsBackToEnglishForUnsetOrUnknown()
{
    Assert.AreEqual("english", EventExhibitionSettings.Parse("1", null, null, false, null, Codes).LanguageCode);
    Assert.AreEqual("english", EventExhibitionSettings.Parse("1", null, null, false, "", Codes).LanguageCode);
    Assert.AreEqual("german", EventExhibitionSettings.Parse("1", null, null, false, "german", Codes).LanguageCode);
    Assert.AreEqual("english", EventExhibitionSettings.Parse("1", null, null, false, "germn", Codes).LanguageCode);
}
```

- [ ] **Step 2: コンパイルして失敗を確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `Parse` の引数数不一致エラー

- [ ] **Step 3: 実装**

`EventExhibitionSettings.cs`:

```csharp
using System;
using System.Collections.Generic;
using Client.Localization;
using UnityEngine;

namespace Client.Starter.EventMode
{
    // イベント出展モードの有効判定と設定値（起動スクリプトが環境変数で注入）
    // Event exhibition mode's enable flag and settings, injected through env vars by the launch script
    public readonly struct EventExhibitionSettings
    {
        private const string EnableEnvKey = "MOORESTECH_EVENT_MODE";
        private const string EditorOptInEnvKey = "MOORESTECH_EVENT_MODE_EDITOR";
        private const string IdleTimeoutEnvKey = "MOORESTECH_EVENT_IDLE_TIMEOUT_SECONDS";
        private const string LanguageEnvKey = "MOORESTECH_EVENT_LANGUAGE";
        private const int DefaultIdleTimeoutSeconds = 180;

        public readonly bool IsEnabled;
        public readonly int IdleTimeoutSeconds;
        public readonly string LanguageCode;

        private EventExhibitionSettings(bool isEnabled, int idleTimeoutSeconds, string languageCode)
        {
            IsEnabled = isEnabled;
            IdleTimeoutSeconds = idleTimeoutSeconds;
            LanguageCode = languageCode;
        }

        public static EventExhibitionSettings FromEnvironment()
        {
            var languageRawValue = Environment.GetEnvironmentVariable(LanguageEnvKey);
            var settings = Parse(
                Environment.GetEnvironmentVariable(EnableEnvKey),
                Environment.GetEnvironmentVariable(IdleTimeoutEnvKey),
                Environment.GetEnvironmentVariable(EditorOptInEnvKey),
                Application.isEditor,
                languageRawValue,
                Localize.GetLanguageCodes());

            // 未知の言語コードは起動を止めずにログだけ残す（起動スクリプトの設定ミス検知用）
            // An unknown language code only leaves a log without stopping boot, to catch launch-script typos
            if (!string.IsNullOrEmpty(languageRawValue) && settings.LanguageCode != languageRawValue)
                Debug.LogError($"EventExhibitionSettings: unknown {LanguageEnvKey}={languageRawValue}, falling back to {settings.LanguageCode}");
            return settings;
        }

        // 有効値は"1"のみ、タイムアウトは正整数のみ受理し他は既定値へ落とす
        // Enable accepts "1" alone; the timeout accepts positive ints only and otherwise falls back to the default
        // Editorは開発機のワールドを不可逆に消すため、専用キーの明示opt-inが無い限り無効にする
        // The Editor wipes a developer's world irreversibly, so it stays off without the dedicated opt-in key
        // 言語は生成テーブルにあるコードのみ受理し他はenglishへ落とす
        // The language accepts only codes in the generated table and otherwise falls back to english
        public static EventExhibitionSettings Parse(string enableRawValue, string idleTimeoutRawValue, string editorOptInRawValue, bool isEditor, string languageRawValue, IReadOnlyCollection<string> supportedLanguageCodes)
        {
            var isEnabled = enableRawValue == "1" && (!isEditor || editorOptInRawValue == "1");
            var idleTimeoutSeconds = int.TryParse(idleTimeoutRawValue, out var seconds) && 0 < seconds ? seconds : DefaultIdleTimeoutSeconds;
            var languageCode = !string.IsNullOrEmpty(languageRawValue) && supportedLanguageCodes.Contains(languageRawValue)
                ? languageRawValue
                : Localize.DefaultLanguageCode;
            return new EventExhibitionSettings(isEnabled, idleTimeoutSeconds, languageCode);
        }
    }
}
```

（`Contains` は `System.Linq` を using する）

`EventModeAutoStart.cs:32` を差し替え:

```csharp
if (!Localize.TrySetLanguage(settings.LanguageCode)) Debug.LogError($"EventModeAutoStart: failed to set language to {settings.LanguageCode}");
```

- [ ] **Step 4: コンパイルとテスト**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "EventExhibitionModeTest"`
Expected: 5 tests PASS

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Starter/EventMode moorestech_client/Assets/Scripts/Client.Tests/EventMode
git commit -m "feat: 出展モードの起動言語をMOORESTECH_EVENT_LANGUAGEで指定できるようにする"
```

### Task 2: gamescom 起動スクリプトでドイツ語を指定

**Files:**
- Modify: `scripts/event/start-gamescom-loop.command:7-10`

- [ ] **Step 1: export 追加**（`MOORESTECH_EVENT_MODE=1` の直後）

```bash
# gamescom はドイツ語で起動する（english / japanese / german）
# gamescom boots in German (english / japanese / german)
export MOORESTECH_EVENT_LANGUAGE=german
```

- [ ] **Step 2: 検証**

Run: `bash -n scripts/event/start-gamescom-loop.command && grep -n "MOORESTECH_EVENT_LANGUAGE=german" scripts/event/start-gamescom-loop.command`
Expected: 構文OK、1行ヒット

- [ ] **Step 3: コミット**

```bash
git add scripts/event/start-gamescom-loop.command docs/adr/0042-event-mode-launch-language-env.md .decisions/2026-08-29-*
git commit -m "feat: gamescom起動スクリプトをドイツ語起動にしADRを追加"
```

### Task 3: 全ブランチレビュー（必須・省略不可）

- [ ] moores-code-review スキルで全ブランチレビューを実行し、指摘を反映してコミットする

## 配置と前例

- 環境変数パース: 既存 `EventExhibitionSettings.Parse`（純粋関数＋`FromEnvironment` で Unity 依存）の形をそのまま拡張。前例同型
- 言語検証の参照先: `Localize.GetLanguageCodes()`（`LanguageSetting.cs:24` が同APIで選択肢を作る前例）。`Client.Starter.asmdef` は既に `Client.Localization` を参照
- LogError の位置: `EventModeAutoStart` の既存 TrySetLanguage 失敗ログと同形

## 判断記録（ADR）

- 設計: `docs/adr/0042-event-mode-launch-language-env.md`、`.decisions/2026-08-29-出展モードの起動言語は環境変数で指定し既定はスクリプト側でgermanにする.md`
- `Parse` に `supportedLanguageCodes` を引数で渡す（`Localize` を Parse 内で直接呼ばない）: agent前提（既存 Parse が `isEditor` を引数で受けて純粋性を保つ前例と同形。テストで生成テーブルへ依存しない）
- LogError を `FromEnvironment` に置く: agent前提（Parse を純粋に保つ Global Constraints から）

## レビュー後の裁定による改修（2026-08-29）

moores-code-review の設計判断 D1〜D4 をユーザー裁定で確定し実装済み（出所: ユーザー裁定 2026-08-29 AskUserQuestion）:
- D1 Localize に集約 → `Client.Localization/LocalizeLanguageApplier.ApplyOrDefault`
- D2 enum で運ぶ → `Client.Localization/LanguageApplyResult.cs`（`LanguageResolution`）
- D3 今回やる → `EventMode/EventModeEnvironmentValues.cs`、`Parse(raw, isEditor)`
- D4 切り出しテスト → `EventModeAutoStart.ApplyLaunchLanguage`、`Client.Tests/EventMode/EventModeLaunchLanguageTest.cs`
これにより本plan Task 1 の「`Parse` に `supportedLanguageCodes` を渡す」「`LanguageCode`」「`FromEnvironment` で LogError」は置き換えられた。
