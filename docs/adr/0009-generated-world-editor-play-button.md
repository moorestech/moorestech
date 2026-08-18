# 0009: 自動生成ワールドの通常プレイはエディタ専用ツールバーボタンで起動する

日付: 2026-08-12
状態: 採択

## 背景

マップ自動生成はサーバー起動時の `WorldProvisioner.EnsureWorld()` に組み込み済みで、`--mapMode generated` を渡せば `MapGenerationPipeline` が地形・鉱脈・木を生成した自動生成ワールドでプレイできる。しかし `StartServerSettings.MapMode` のデフォルトは `template`（既成map.jsonのコピー）であり、メインメニューの「ローカルで開始」（`StartLocal`）はサーバー起動引数を一切渡さないため、通常プレイでは自動生成ワールドに到達できない。

前例として `NoSaveLoadPlayToolbarElement` がある。エディタツールバーに専用再生ボタンを追加し、SessionStateフラグを立てて `GameInitializer` シーンからPlayModeへ入り、`InitializeScenePipeline` 内の `SkipSaveLoadPlayModeSettings.ApplyIfNeeded()` が `CreateLocalServerArgs` を書き換える。起動引数の書き換えだけで、以降のサーバー起動・プロビジョニング・セーブロード・接続は通常プレイと完全同一経路になる。

## 決定

1. **エディタツールバーに generated 起動ボタンを追加する。メインメニューUIは作らない。**
   NoSave Play と同型の2ファイル構成（ツールバー要素＋`ApplyIfNeeded` 型の引数書き換えクラス）とする。
   出所: ユーザー裁定 2026-08-12（[[2026-08-12-generatedワールドプレイはエディタ専用ボタンで提供する]]）
2. **書き換える起動引数は `WorldDirectory = <saves>/world_generated` と `MapMode = generated` の2項目のみ。**
   AutoSave はデフォルト（有効）のまま。ゲームプレイ経路は通常プレイと完全同一であることをユーザーへ提示し確認済み。
   出所: ユーザー裁定 2026-08-12（「実際のゲームプレイと同じ経路になるの？」への確認を経て採択）
3. **ワールドは専用ディレクトリ world_generated へ1回生成して永続化し、以後は続きからプレイする。**
   `WorldProvisioner` の「world.json があれば no-op」挙動をそのまま使う。毎回使い捨て生成は却下。
   出所: ユーザー裁定 2026-08-12（過去裁定「新規作成時1回生成し永続化」と整合）
4. **作り直しは確認ダイアログ付きメニュー項目「Delete Generated World」で行う。起動ボタンは1つのまま。**
   出所: ユーザー裁定 2026-08-12（2ボタン案・手動削除のみ案を却下）
5. **seed指定UIは作らない。** `ServerInstanceManager` の「未指定なら起動時採番」をそのまま使い、seedを変えたいときは削除→再起動で回す。
   出所: agent前提（後方互換・将来拡張性を壁打ち段階で考慮しない原則）
6. **再生終了時にSessionStateフラグと `playModeStartScene` を復元し、通常の再生ボタンに影響させない。**
   出所: agent前提（`NoSaveLoadPlayToolbarElement.OnPlayModeStateChanged` の既存パターン踏襲）

## 却下した選択肢

- **メインメニューUIでのマップモード選択**: UIはWeb移行済みのためWeb UI側の設計が必要になり一段大きいタスクになる。必要になった時点で別途起こす。ボタンと同じ引数をメニューから渡すだけで移行できる構造は保たれる
- **毎回使い捨て生成（一時ディレクトリ・保存無し）**: 継続プレイ不可。過去裁定「seed毎回再生成は却下」と不整合
- **起動ボタン2つ（続きから／新規生成）**: ツールバーが混む。削除メニューで代替可能
- **サーバー側デフォルト（`StartServerSettings.MapMode`）の generated 化**: 既存の world_1（template）運用・テスト・プレイテストDSLへ波及するため今回のスコープでは変えない

## 影響

- 変更はすべてエディタ専用コード（Editor asmdef / `#if UNITY_EDITOR`）。ビルド・サーバー・プロトコルへの影響ゼロ
- 将来メインメニューUIを作る場合も、同じ起動引数を渡すだけでよい
