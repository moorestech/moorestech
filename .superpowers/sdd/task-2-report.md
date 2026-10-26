# Task 2 実装報告

ステータス: DONE_WITH_CONCERNS（Web単体・型検査・lint成功。PlaywrightはwebServer起動待ちで失敗。C#コンパイルはTask 4で実施）

## 実装・突合結果
既存WIP 768008155aをbase edd6ed84a9からStep 1〜6と突合した。
- Step 1: NotificationMessagePackのKey(2)を保ちDictionary<string,string>化。全factory呼出しを辞書へ。research/challenge/count/targetとsave migrationの3件数の名前が生産者と一致。
- Step 2: TooltipTopicはToDictionary(Name,Value)、DTOはIReadOnlyDictionary。NotificationTopic DTOはDictionary。
- Step 3: Client.Playtestの3本は型追随のみ。TooltipHasParamの呼出しは範囲内ブロック数なのでcountで正しい。MaterialShortageLinesの呼出しはCount確認と文字列診断だけで、要素値への比較処理は無い。
- Step 4: Web schemasとstoreはrecord。名前付き補間を直接渡し、buildPositionalInterpolationValuesとexportを削除。研究とチャレンジは名前付きGUIDをWeb辞書で表示名へ解決。
- Step 5: 配列拒否・名前付き補間・GUID名前解決・名前欠落警告のテストとe2e fixtureを追随。取り残されていたCursorTooltip.test.tsの日本語コメントを名前付きへ訂正。
- Step 6: 追加修正と本報告をコミット・push。bdへレーン境界と結果を記録。

## Task 1 Importantの修正確認
MouseCursorTooltipState.cs:24はShow(owner,key,Array.Empty<LocalizationTextParam>())。
同ファイル27行の受け側IReadOnlyList<LocalizationTextParam>と一致し、29行のTooltipLineも同型を受ける。using Client.Localizationも存在する。
全tracked .csでArray.Empty<string>()・MessageParams/TextParams・factory呼出し・GetFormatted/new TooltipLine/HintParamsを検索。残存Array.Empty<string>()は文字列一覧用途で、通知・ツールチップの旧型呼出しは見つからなかった。
これはソース照合の結果であり、Unityコンパイルの実測ではない。Task 4のコンパイルと独立レビューで解消を確定する。

## 既定判断とレーン境界
- deleteUnconfirmed/deleteRejectedのCSVは全5言語列が{target}で、DeleteResultFeedbackのtarget辞書と一致。WIPで実施済み。
- server-notification-smoke.csは元の検証の意味を維持し、researchキーの値が「原始研究1」であることを検査する未コミット版を採用した。現在サーバーはGUIDを送るため、この旧来検証の整合はTask 6の録画と旧履歴確認へ引き継ぐ。検証をキー存在だけへ弱めない。
- BのInitializeScenePipeline.csはTask 1の型追随。CのTooltipTopic/NotificationTopic、FのClient.Playtest 3本とserver-notification-smoke.csは型追随のみ。設計変更はしていない。
- Unity起動・Editor-slot操作は行わなかった。

## 検証
作業ディレクトリ: moorestech_web/webui。
- `PATH="$PWD/../node/mac-arm64/bin:$PATH" pnpm test`: 同梱Node v20.18.1では144 files / 1098 tests成功後、jsdom環境4件がERR_REQUIRE_ESM。html-encoding-snifferが@exodus/bytes/encoding-lite.jsをrequireする箇所で失敗（/tmp/uitk-task2-vitest.log:708〜）。
- `/opt/homebrew/bin/pnpm test`（PATH既定の/opt/homebrew/bin/node v26.12.1）: 148 files / 1142 tests成功、終了0。ログ /tmp/uitk-task2-vitest-node26.log。
- `PATH="$PWD/../node/mac-arm64/bin:$PATH" npx tsc --noEmit -p tsconfig.json`: 成功。
- `PATH="$PWD/../node/mac-arm64/bin:$PATH" npx tsc --noEmit -p e2e/tsconfig.json`: 成功。
- `PATH="$PWD/../node/mac-arm64/bin:$PATH" pnpm lint`: 成功。
- `pnpm test:e2e --global-timeout=180000`（Node26）を1回実行。開始時5273にlistener無し。`Error: Timed out waiting 120000ms from config.webServer.`で終了1。実行中観測ではwebServerの`tsc -b` PID44578が進行中で、テスト本体へ未到達。ブラウザ不足の証拠は無い。ログ /tmp/uitk-task2-e2e.log。
- `git grep -nE "buildPositionalInterpolationValues|\{p[0-9]|\bp0\b" -- moorestech_web/webui/src moorestech_web/webui/e2e`: 0件。
- textParams/messageParams配列は配列拒否テスト2か所のみ。変更コードは全ファイル200行未満。git diff --check成功。

## 自己レビュー・懸念
ソース突合で追加の型追随漏れは見つからなかった。CSVのtarget変更はPR本文のユーザー確認事項へ載せる。Node20の依存ロード失敗、Playwright起動タイムアウト、server-notification-smokeの旧検証とGUID送信の整合は後工程への明示事項。今回の補間器・wire設計は変えていない。

## 変更ファイル（WIPからのTask 2全体）
- .agents/skills/unity-playmode-recorded-playtest/scenarios/server-notification-smoke.cs
- Localization/localization.csv
- moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Copy/BlueprintCreateFailureNotifier.cs
- moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Undo/Removal/VanillaRemovalRestoreSender.cs
- moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Tooltip/MouseCursorTooltipState.cs
- moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Completion/DeleteResultFeedback.cs
- moorestech_client/Assets/Scripts/Client.Playtest/Operations/Ui/BlueprintCopyPasteScenarioChecks.cs
- moorestech_client/Assets/Scripts/Client.Playtest/Operations/Ui/BlueprintPasteGhostProbe.cs
- moorestech_client/Assets/Scripts/Client.Playtest/Operations/Ui/DeleteBoxScenarioInput.cs
- moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Blueprint/Copy/BlueprintCreateFailureNotifierTest.cs
- moorestech_client/Assets/Scripts/Client.Tests/UIState/DragDelete/Completion/TrainCarRemovalRequestsTest.cs
- moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireContractNotificationTest.cs
- moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/notification_item_earned.json
- moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/notification_save_migration.json
- moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/C2/TooltipTopic.cs
- moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/Notification/NotificationTopic.cs
- moorestech_server/Assets/Scripts/Server.Event/Notification/AchievementNotificationWiring.cs
- moorestech_server/Assets/Scripts/Server.Event/Notification/NotificationMessagePack.cs
- moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/BlueprintProtocol.cs
- moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/CompleteResearchProtocol.cs
- moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/ElectricWireDisconnectProtocol.cs
- moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/ElectricWireExtendProtocol.cs
- moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/GearChainConnectionEditProtocol.cs
- moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/MiningProtocol.cs
- moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/OneClickCraft.cs
- moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/PlaceBlockProtocol.cs
- moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Rail/RailConnectByDestinationProtocol.cs
- moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RailConnectionEditProtocol.cs
- moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RemoveTrainCarProtocol.cs
- moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Blueprint/BlueprintPasteOperationHandler.cs
- moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/InventoryService/InventoryItemMoveRejectionReporter.cs
- moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/BlueprintProtocolTest/BlueprintPasteProtocolTestContext.cs
- moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/ElectricWireAutoConnectPlaceTest/PlaceBlockNoAutoConnectWiringTest.cs
- moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/Event/AchievementNotificationWiringTest.cs
- moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/Event/MissingMasterPruneNotificationTest.cs
- moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/Event/NotificationServiceTest.cs
- moorestech_web/webui/e2e/captures/capture-connection-line-notifications.ts
- moorestech_web/webui/e2e/mock-host/topics/topicControls.ts
- moorestech_web/webui/src/bridge/contract/schemas/__tests__/ui.test.ts
- moorestech_web/webui/src/bridge/contract/schemas/ui.ts
- moorestech_web/webui/src/bridge/contract/validators.test.ts
- moorestech_web/webui/src/bridge/store/topicEventDelivery.test.ts
- moorestech_web/webui/src/features/notification/notificationMessages.test.ts
- moorestech_web/webui/src/features/notification/notificationMessages.ts
- moorestech_web/webui/src/features/notification/notificationStore.test.ts
- moorestech_web/webui/src/features/notification/notificationStore.ts
- moorestech_web/webui/src/shared/i18n/i18nStore.ts
- moorestech_web/webui/src/shared/i18n/index.ts
- moorestech_web/webui/src/shared/tooltip/CursorTooltip.test.ts
- moorestech_web/webui/src/shared/tooltip/CursorTooltip.tsx

継続追加: CursorTooltip.test.tsのコメント、server-notification-smoke.csの値検証、本報告。
