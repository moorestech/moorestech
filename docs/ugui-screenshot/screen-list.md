# moorestech UI 画面一覧（Web移行用リファレンス）

UIをWebへ移行する準備として、現行Unityクライアントの全画面を洗い出し、実際にPlayModeで開いてスクリーンショットを取得した記録。
This document enumerates every UI screen in the current Unity client and records screenshots actually captured in PlayMode, as reference material for the web-UI migration.

- スクリーンショット保存先: `docs/ugui-screenshot/screenshots/`
- 解像度: 1104×387（GameView rendering解像度。Unity CLI Loop skill既定値、全画面共通）
- 取得方法: すべて実際にUnity Editor PlayMode上で対象UIを開いた状態を撮影（「推定」ではない）。主要画面はキー入力（InputSystem.QueueStateEvent注入）またはUIStateControlへのリフレクション強制遷移で到達し、`uloop screenshot --capture-mode rendering` で撮影。

## 凡例 / Legend
- **状態**: ✅取得済み / ⛔未取得（理由記載）
- **到達方法**: 実際に使った具体的な手段

---

## A. 起動時・メタ画面 / Bootstrap & meta screens

| # | 画面名 | 状態 | スクリーンショット | シーン/根拠 | 到達方法 |
|---|---|---|---|---|---|
| 1 | タイトル画面 (MainMenu) | ✅ | `01-main-menu-title.png` | `Assets/Scenes/Game/MainMenu.unity` | シーンをEditorSceneManager.OpenSceneで開きPlayMode起動 |
| 2 | ローディング画面 (GameInitialaizer) | ✅ | `02-loading-screen.png` | `Assets/Scenes/Game/GameInitialaizer.unity`、`InitializeScenePipeline.cs` | MainMenuで「ローカルサーバーに接続」ボタンをUI経由クリック（`uloop simulate-mouse-ui`）→ 自動遷移 |
| 3 | サーバー接続エラーポップアップ (ServerConnectPopup) | ⛔ | — | `Client.MainMenu/PopUp/ServerConnectPopup.cs`、GameObject `Canvas/TitleMenu/ServerConnectPopup` | **未取得**: トリガーとなる「Connect server」ボタン (`Canvas/TitleMenu/Connect server`) が現在のUIビルドで`activeSelf=False`（非表示・到達不能）。現行プレイヤーからは到達できない機能と判明。強制的にactive化すれば表示自体は可能だが、実質死んでいる導線のため優先度低と判断し見送り |
| 4 | ネットワーク切断パネル (NetworkDisconnectPresenter) | ✅ | `05-network-disconnect.png` | `Client.Game.InGame.Presenter.PauseMenu.NetworkDisconnectPresenter` | MainGame中に`disconnectPanel`をリフレクションで直接`SetActive(true)`（実切断せず表示のみ強制） |

---

## B. コアUIStateEnum画面（ゲーム内） / Core UIStateEnum screens (in-game, MainGame scene)

`Assets/Scripts/Client.Game/InGame/UI/UIState/UIStateEnum.cs` の11状態。`UIStateControl`へのリフレクション強制遷移（`UIStateDictionary.GetState(state).OnExit/OnEnter` + `CurrentState`の private setter書き換え）で全状態に到達。

| # | 状態 (enum) | 状態 | スクリーンショット | 到達方法 |
|---|---|---|---|---|
| 5 | GameScreen（通常HUD） | ✅ | `03-gamescreen-hud.png` | デフォルト状態。左下にKeyControlDescription（キー案内）も同時に写っている |
| 6 | PlayerInventory | ✅ | `04-player-inventory.png` | Tabキーを`InputSystem.QueueStateEvent`で注入（押下→0.8秒保持→離す） |
| 7 | SubInventory（ブロック別インベントリ、代表12種） | ✅ (12種) | `06`〜`17-subinventory-*.png` | 詳細はセクションCを参照 |
| 8 | PauseMenu | ✅ | `18-pause-menu.png` | Escapeキーを注入 |
| 9 | DeleteBar（破壊モード） | ✅ | `19-delete-bar.png` | UIStateControlへリフレクション強制遷移（Gキーは legacy `Input.GetKeyDown` 実装のためInputSystem注入不可と判明、代替） |
| 10 | Story（スキット/カットシーン） | ⛔ | — | **未取得**: `SkitManager.StartSkit("Vanilla/Skit/skits/100_start_game")` および `sample_short` を直接呼び出したが、いずれも `NullReferenceException` と `SkitControllableObject with ID 'SpaceSkybox'/'Planet_2' not found` の警告が出て `IsPlayingSkit` が常に `False` に戻る。ゲーム開始直後の専用シーケンス外から起動すると必要な環境オブジェクトが揃わない模様。加えて本worktreeには `moorestech-client-private`（非公開クライアントアセット）が未展開で、一部アセットが欠けている可能性もある |
| 11 | PlaceBlock（設置モード） | ✅ | `20-place-block.png` | UIStateControlへリフレクション強制遷移（Bキーもlegacy Input実装のため） |
| 12 | ChallengeList | ✅ | `21-challenge-list.png` | UIStateControlへリフレクション強制遷移（Tキーもlegacy Input）。**注記**: 新規ノーセーブ状態のため中身が空。パネル外枠のみの参考画像 |
| 13 | ResearchTree | ✅ | `22-research-tree.png` | UIStateControlへリフレクション強制遷移（Rキーもlegacy Input）。研究ツリーのノード・接続線まで正常描画を確認 |
| 14 | Debug（ブロック情報デバッグモード） | ✅ | `23-debug-block-info.png` | UIStateControlへリフレクション強制遷移（F3キーもlegacy Input） |
| 15 | TrainHUDScreen（列車搭乗中） | ⛔ | — | **未取得**: 到達には実際に機能するレール網＋列車車両の設置（レール座標・コネクタoffsetの計算を要する複雑な設定）が必要。`RideTrainCarRequest`経由のUIStateContext要求も含め、スクリーンショット取得のためだけに構築するコストが見合わないと判断し見送り。列車システム専用の`train-rail-*`スキルを使った別セッションでの取得を推奨 |

### C. ネスト状態（TrainHUDScreen配下） / Nested sub-states

| # | サブ状態 | 状態 | 理由 |
|---|---|---|---|
| 16 | TrainHUD内 GameScreen | ⛔ | 親のTrainHUDScreen自体が未到達のため連動して未取得 |
| 17 | TrainHUD内 PauseMenu | ⛔ | 同上。加えてリフレクション階層がさらに1段深い（`_subStateController`→`_states`辞書） |

---

## C. SubInventory（ブロック別インベントリ）詳細 / Block-specific SubInventory screens

`moorestechAlphaMod_8/master/blocks.json` を全件走査し、`blockUIAddressablesPath` が異なる distinct な12種類のUIプレハブを特定。各代表ブロックをサーバーAPI (`ServerContext.WorldBlockDatastore.TryAddBlock`) で直接設置し、クライアント側の`BlockGameObject`生成を待った上で`SubInventoryState`をリフレクション強制遷移（`BlockSubInventorySource`経由）して撮影。ワールド上への実配置は不要（クリック操作を介さず直接生成）。

| # | Addressableパス | 状態 | スクリーンショット | 代表ブロック |
|---|---|---|---|---|
| 7-1 | `Vanilla/UI/Block/ChestBlockInventory` | ✅ | `06-subinventory-chest.png` | 木のチェスト |
| 7-2 | `Vanilla/UI/Block/MinerBlockInventory` | ✅ | `07-subinventory-miner.png` | 風力掘削機（電動） |
| 7-3 | `Vanilla/UI/Block/MachineBlockInventory` | ✅ | `08-subinventory-machine.png` | 石窯（電動汎用機械） |
| 7-4 | `Vanilla/UI/Block/GearMachineBlockInventory` | ✅ | `09-subinventory-gearmachine.png` | 原始的な粉砕機（歯車機械、トルク/回転数UI付き） |
| 7-5 | `Vanilla/UI/Block/GearEnergyTransformerUI` | ✅ | `10-subinventory-gearenergytransformer.png` | 木のシャフト |
| 7-6 | `Vanilla/UI/Block/GearMinerBlockInventory` | ✅ | `11-subinventory-gearminer.png` | 原始的な採掘機（歯車採掘） |
| 7-7 | `Vanilla/UI/Block/GeneratorBlockInventory` | ✅ | `12-subinventory-generator.png` | 燃料式風車（発電機） |
| 7-8 | `Vanilla/UI/Block/TrainItemPlatformBlockInventory` | ✅ | `13-subinventory-trainitemplatform.png` | 貨物プラットフォーム（液体プラットフォームも同一UIを共用） |
| 7-9 | `Vanilla/UI/Block/TrainStationBlockInventory` | ✅ | `14-subinventory-trainstation.png` | 蒸気機関車駅（TrainItemPlatformとほぼ同一UI） |
| 7-10 | `Vanilla/UI/Block/FilterSplitterBlockInventory` | ✅ | `15-subinventory-filtersplitter.png` | フィルター分岐器（出力1〜3のフィルタ設定UI） |
| 7-11 | `Vanilla/UI/Block/ElectricPoleNetworkInfoUI` | ✅ | `16-subinventory-electricpole.png` | 電柱（発電量/要求量ネットワーク情報） |
| 7-12 | `Vanilla/UI/Block/ElectricToGearBlockInventory` | ✅ | `17-subinventory-electrictogear.png` | 回転生成機（電力→歯車変換） |
| — | `Vanilla/UI/Block/BaseCampInventory` | ⛔ | — | **未取得**: `blocks.json`（v8 mod）に `BaseCamp` 系ブロックのエントリが存在しない。このMoDバージョンでは未実装/未使用と判明 |

---

## D. オーバーレイ・補助UI / Overlay & supporting UI

UIStateEnum外だが視覚的に独立したUI要素。

| # | 要素 | 状態 | スクリーンショット | 到達方法 |
|---|---|---|---|---|
| 18 | コンテキストメニュー（右クリックメニュー） | ✅ | `24-context-menu.png` | `ContextMenuView.Instance.Show(null, [...])` を直接呼び出し。位置決定が実OSマウス座標依存（legacy `Input.mousePosition`）だったため、`menuParent`をリフレクションで画面中央へ再配置 |
| 19 | 共通モーダル (CommonModal/OneButtonModal) | ⛔ | — | **未取得**: `ModalManager.OpenModal`を呼び出す実装（`IModalInstantiator`の具象クラス）がコードベース中に一件も存在しないことを確認（未使用/未配線のデッドコード）。ゼロから`IModalInstantiator`を実装するコストが見合わないため見送り |
| 20 | マウスカーソルツールチップ | ✅ | `26-mouse-cursor-tooltip.png` | `MouseCursorTooltip.Instance.Show(text, isLocalize:false)`。位置追従コンポーネント`UICursorFollowControl`がlegacy `Input.mousePosition`依存でUpdate毎に上書きするため、`enabled=false`にしてから画面中央へ固定配置 |
| 21 | キー操作ヒントバー (KeyControlDescription) | ✅（写り込み） | 各GameScreen系スクショの左下 | 独立画面ではなく常時表示のオーバーレイ。状態ごとに文言が変化することを`03/18/19/20/23`等で確認済み |
| 22 | チュートリアルハイライト (TutorialHighlight) | ⛔ | — | **未取得**: 初回プレイ限定のチュートリアル演出。`PlayerPrefs`/チュートリアルフラグのリセットが必要で、時間の都合上見送り |
| 23 | HUDアロー（目的地方向インジケータ） | ⛔ | — | **未取得**: チュートリアル/ミッションの目標が有効な時のみ表示。有効なミッション状態の構築が必要で見送り |

---

## まとめ / Summary

- **画面候補総数**: 23（UIStateEnum 11種 + ネスト2種 + SubInventory 13種[12実装+BaseCamp] + Bootstrap/メタ4種 + オーバーレイ6種、重複整理後）
- **取得済みスクリーンショット数**: 25枚（SubInventoryの12バリアント込み）
- **未取得**: 8項目（ServerConnectPopup、Story/Skit、TrainHUDScreen×2、BaseCampInventory、CommonModal、TutorialHighlight、HudArrow）— いずれも理由を明記
- **既知の描画上の注意点**: 本worktreeには地形テクスチャ等の一部アセット（`moorestech-client-private`）が展開されておらず、地面がUnityデフォルトのチェッカーテクスチャで表示されている。UI要素自体の見た目・レイアウトへの影響はない
