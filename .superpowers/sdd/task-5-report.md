# Task 5 旧SkitUI撤去

ステータス: DONE_WITH_CONCERNS（Unity形式移行差分のみ。検証は完了）

## 実装
- MainGameUI.prefabのSkitUI、SkitTestのSkitUI、StoryTestのStoryUIをUnity Editor APIで除去。GameSystemはネスト元の変更と再シリアライズで参照を掃除。
- 実測: StoryUIのparent=MainGameUI、source=Assets/Asset/Skit/SkitUI.prefab。親Canvasを削除していない。
- SkitManager/起動DI/デバッグDI/StoryContextExtensionの旧SkitUI参照とSkitPresentationModeを除去。
- Text/Selection/Transition/ShowTextコマンドの既存Web分岐だけを残し、音声・skip・待機処理は変更しない。
- 旧SkitUI/SkitUITools/SkitPresentationModeと旧NewUI/Skit資産（meta含む）を削除。
- SkitFailureCleanupTestは旧UI生成とassertだけ除去。SkitCommandLocalizationTestの旧UI共有/voice2回/labels3件という死経路の期待をWeb単独へ追随。元のvoice source保持、3選択肢、固定field名の検査は維持。
- SkitManagerは空行を整理して198行。既存partialコマンド宣言を継続し、新設partial/Funcなし。

## Unity保存と復旧
- Unity固有ファイルをテキスト編集・hunk編集していない。
- StoryTestは全体をgit restoreした後、Unity APIで1個のStoryUIインスタンスだけを再削除・SaveSceneしたが、初回と同じ30追加/136削除になる。RenderSettings/LightmapSettings/Light/MeshRendererのUnity形式移行が保存に伴う。差分はそのまま残した。
- MainGameUIは保存によりCefUnityの_enableLog→_logLevel移行が1件発生。旧SkitUI削除以外の新しい設定判断は行っていない。
- ForceReserializeで生じたMainGame.unityの無関係参照整理はファイル全体をgit restoreして除外。
- StoryTest全戻し時の外部シーン変更がEditorを停止。sampleでEditorSceneManager::HandleOpenScenesChangeOnDisk→NSAlert runModalを観測。CLI44334/44326と自身Editor34274を指定killし、uloop launchでEditor76683へ復旧。他Editor16055は操作していない。Editor76683はTask6用に残す。
- 再保存のため旧prefab/metaを一時復元し、Unityからsourceを実測して削除後に再削除済み。meta手作成なし。

## 検証
- compile-final.json: ErrorCount0/WarningCount0。
- テスト期待追随後compile-test-update.json: ErrorCount0/WarningCount46（既存警告）。
- 全削除資産metaから採った20GUIDを全prefab/unity/assetで検索して0件。指定3GUIDも0件。C#のSkitUI/SkitPresentationMode/GetSkitUI残存0件（撤去非存在を検証するテスト文字列は除く）。
- git diff --check成功。
- 誤採用訂正: 最初に330成功とした共有TestResults.xmlはClient.Tests.Playtest.*を含む別Editorの結果だった。Task5の成功証拠として不採用。
- Skit regex実行はPlayMode遷移のdomain reloadでCLIがUNITY_DISCONNECTED_AFTER_ACCEPTとなるため、マシン共通XMLのstart-timeと実行クラスを確認して回収。
- 09:25:20Z〜09:25:50Zの正しいSkit結果は87件/84成功/3失敗。失敗はすべてSkitCommandLocalizationTestの旧UIを期待したsource検査。上記のとおり追随し再実行。
- 再起動直後runではlilToon Hidden/ltspass_lite_transparentの_MAIN_LIGHT_SHADOWS再定義がIPostBuildCleanupでUnhandledLogMessageExceptionになった。プロダクト/依存shaderを変更せず、import完了後に同じ全regexを再実行。最終XMLにshader失敗が残るかを確認する。

## 証拠
raw: /Users/sakastudio/hermes-agent/data/repos/moorestech_logs/harness/sdd/feature-uitk-s0-wave2/task-5/
- remove-assets.json、reserialize.json、storytest-first-save.diff、storytest-targeted-save.json
- editor-stall.sample.txt、relaunch.json
- compile.json、compile-final.json、compile-test-update.json
- tests-skit*.json、TestResults-skit-retry.xml（87件3失敗）、TestResults-skit-updated.xml（最終）
- guid-scan.txt、logs-final.json
- TestResults.xml（他Editor330件、不採用）/TestResults-final.xml（他Editor古い87件、不採用）

## 自己レビュー
既存Web経路の純削除で挙動を維持。選択肢jump対応、音声原文lookup、SkitFailureCleanupの後始末は残る。try/catch、失敗ガード、キャンセル仕様の追加・変更なし。Unity形式移行差分は不可避という実測を上記に残す。

## 最終結果
- Skit全regexの最終XMLは09:27:21Z〜09:27:52Z、87/87成功、失敗0、skip0。SkitFailureCleanupTest・SkitCommandLocalizationTest・SkitWorldObjectRegistrationTestを含む。CLIはdomain reloadによるEOFのためXMLで判定した。
- テスト後Consoleのreload failed1件は既存SkitLocalizationResolverLifecycleTest.cs:147のLogAssert.Expectと151の意図的例外注入に一致（stack付きログ保存済み）。LogAssert追加・エラー無視の追加なし。
- import完了後の最終Skit全regexではshader失敗が再発していない。変更なしの既存lilToon shaderをテスト起動時importがコンパイルしたログが初回障害の観測点で、恒常故障ではなかった。Task6では再発有無を引き続き観測。
- 既存テストの期待例外を証拠保存後にConsole clear→compile→get-logsでError0を確認。
- Editor76683は保存済み空シーンでTask6へ引き継ぐ。起動receiptはuloop launchで登録。
