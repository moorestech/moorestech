# 0066. エディタの手動直Playは常時記録を有効にする（無人起動だけ除外）

日付: 2026-09-21
状態: 採択（ADR 0057「常時記録の有効化は…通常起動はオン、NUnitテストとプレイテストDSLはオフ」の「通常起動」にエディタの直Playを含める形で補う）

## Context

常時記録の決定は `AlwaysOnCaptureSetting` 1つが持ち、既定は無効。有効化するのは `LocalGameLauncher.StartLocalGame()`（メインメニューのローカル開始ほか）と `ConnectServer` だけだった。エディタで GameInitializer シーンを直接Playすると `InitializeScenePipeline` は走るがこの2経路を通らず、常時記録が無効のまま走る。

2026-09-21、直Play中にバグ報告を出したところ、箱は書けたが manifest の video／serverSnapshot／進行記録 reportSent が全欠損した。開発中のプレイの大半は直Playなので、開発者が遭遇したバグの報告はほぼ常に空になる。

一方、EditModeInPlayingTest と unityプレイ録画テスト（DSL）も直Playと同じ `InitializeScenePipeline` 経路で起動する。Unity 上、人が押したPlayと `EditorApplication.isPlaying = true` によるPlayは積極的には区別できない。

## Decision

- **エディタで人がPlayボタンを押した直Playは、無条件で常時記録を有効にする。** 開発中の毎Playで録画リング・スナップショットリングが回り、手元の `ProgressRecords/` に開発セッションが1件ずつ溜まることを受け入れる。
  出所: ユーザー裁定 2026-09-21 原文「エディタ直Playでも常時記録を入れたい」→ 質問「直接Playしたとき、常時記録はどの条件で有効になるべきですか？」→ 選択「手動の直Playは常に有効」
  棄却案: 永続トグルでopt-in（EditorPrefs・既定OFF。OFFのまま報告すると同じ欠損が再発する）／専用再生ボタンで1回だけ有効（バグに遭遇してから報告しても空のまま）

- **除外は自動起動側の宣言で行い、印の無い直Playは全て有効とする。エージェントが uloop control-play-mode／execute-dynamic-code で起動した素のPlayも、人のPlayと同じ扱いで記録する。** 将来の自動起動経路が宣言を忘れた場合に記録が始まることも受け入れる。
  出所: ユーザー裁定 2026-09-21 質問「エージェントが uloop … で起動したPlayは印が無いので記録されます。これでよいですか？」→ 選択「記録してよい」
  棄却案: uloop起動も除外する（Play前に印を立てる手順をスキルへ追記して運用で担保。コードだけでは保証できない）

- **除外の判定は既存の「無人起動」の印（`PlaytestStartGateBypass`）を再利用する。「無人起動なら、直Playでも常時記録を自動では有効にしない」の1ルール。** EditModeInPlayingTest とプレイ録画テストDSLは既にこの印を立てているので、自動起動側の変更は無い。開始ゲートと常時記録という2つの関心が「無人起動」1概念に結合することを受け入れる。
  出所: ユーザー裁定 2026-09-21 質問「直Playの自動有効化を止める判定はどちらにしますか？」→ 選択「既存の無人起動の印を再利用」
  棄却案: 常時記録専用の「記録しない」印を新設（自動起動の入口が印を2つ立てる義務を負い、片方忘れが無音で起きうる）

- **ツールバーの専用再生ボタン2種（生成ワールドでPlay／セーブ無しでPlay）からの起動も、常時記録を有効にする。** セーブ無しPlayは「通常セーブは読まない・書かないが、バグ報告用のスナップショットリング・録画・`ProgressRecords/` は書く」動作になる。セーブ無しPlayで常時記録が無効であることを確かめている既存テストは書き換える。
  出所: ユーザー裁定 2026-09-21 質問「専用再生ボタン2種（生成ワールドでPlay／セーブ無しでPlay）から起動したときも、常時記録を有効にしますか？」→ 選択「両方とも有効」
  棄却案: 生成ワールドのみ有効（セーブ無しPlay中の報告が今回と同じく欠損する）／専用ボタンは両方とも対象外

- **無人起動のときは「有効にしない」だけで、無効へ上書きはしない。** 無人起動でも記録したいテスト（`PlaytestReportAndProgressTest`）は起動前に明示的に `Apply(Enabled())` しており、これを潰さない。有効にしなかった理由はログへ出す。
  出所: agent前提（既存テスト `PlaytestReportAndProgressTest` の前例・AGENTS.md「無音の縮退は禁止」）

- **有効化の置き場所はエディタ専用の起動上書きの束 `PlayModeLaunchOverrides`（`InitializeScenePipeline` から `#if UNITY_EDITOR` で呼ばれる）。** 決定そのものは引き続き `AlwaysOnCaptureSetting.Apply` 1口を通す。メインメニュー経由の起動では二重に Apply されるが結果は同じ。
  出所: agent前提（`SkipSaveLoadPlayModeSettings`／`GeneratedWorldPlayModeSettings` と同型の前例）

- **無人起動の印を消費するのは従来どおり開始ゲートだけ。常時記録の判定は印を消費せずに覗く。** 起動上書き（`InitializeScenePipeline` 序盤）は開始ゲート（`MainGameInitializationFinalizer` 終盤）より先に走るので、ここで消費するとゲートが印を見失い、無人起動が応答待ちで恒久停止する。
  出所: agent前提（`PlaytestStartGateBypass` の消費セマンティクスと、両者の呼び出し順の実測）

- **エディタ直Playの進行記録は既存の `ProgressRecords/` にそのまま書く。保存先は分けない。** 記録には `BuildInfo`（エディタでは null）が入るので、開発セッションは後から見分けられる。
  出所: ユーザー裁定 2026-09-21（第1問の選択肢文に帰結「手元のProgressRecords/に開発セッションが毎回1件ずつ溜まる」を明記した上での採択）。見分けられる点は agent前提（`ProgressRecorder` が `RepositoryStateProbe.ReadBuildInfo()` を記録している実装）

- **バグ報告のアップロード判定（エディタは buildInfo 無し → DeveloperMode で outbox 止まり）は変更しない。**
  出所: agent前提（依頼の範囲外）

## Consequences

- `AlwaysOnCaptureSetting`・`LocalGameLauncher`・`ConnectServer` にある「本番のプレイ開始だけが有効にする」旨のコメントと、`ProgressRecorder` の「調査用・テスト用の起動まで書き始める」旨のコメントは実態と合わなくなるので書き換える。
- 常時記録が無効であることを確かめている既存テストのうち、`EditModeInPlayingTestUtilTest` は無人起動の印の下で引き続き無効であることを確かめる形で維持し、`SkipSaveLoadPlayModeSettingsTest` はセーブ無しPlayでも有効になる裁定に合わせて書き換える。
