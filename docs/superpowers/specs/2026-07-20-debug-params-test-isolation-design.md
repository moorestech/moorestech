# デバッグ設定のテスト隔離（DebugParameters キャッシュ環境切替）設計

日付: 2026-07-20
ステータス: 設計承認済み（実装前）

## 背景・問題

`DebugParameters`（`moorestech_server/Assets/Scripts/Common.Debug/DebugParameter.cs`）は
`../cache/{Bool,Int,String}DebugParameters.json` を読み書きする静的クラスで、
クライアント・サーバー・プレイテストDSLが共有している。

2026-07-20、プレイテストDSL（`PlaytestSetup.SetupDebugEnvironment`）が書いた
`FreeBlockPlacement: true` が cache に残置され、`PlaceBlockProtocol` が解放・建設コスト・
電線自動接続を全てスキップする状態になり、`ElectricWireAutoConnectPlaceTest` 3件が
「例外なしのアサート失敗」で落ちた（マージ起因と誤認しかけた）。

既存の防御は2系統あるがどちらも保証にならない:

- `PlaytestSetup` は次回実行時に false を明示上書きする（= 次回まで漏れ続ける。DSL を使わない
  ユニットテストには効かない）
- `PlaytestBoot` は `DebugServerDirectory` のみ SessionState 退避 → 再生終了時復元
  （コミット 4f919ee70）。クラッシュ・強制終了で復元が走らないと残置する。
  `FreeBlockPlacement` は復元対象外

根本原因は「開発者の永続デバッグ設定」と「テスト・自動プレイテストの実行時設定」が
同一ファイルを共有していること。

## ゴール

1. ユニットテスト（NUnit）が開発者環境のデバッグ設定に一切影響されない（hermetic）
2. テスト経由の初期化はすべて隔離環境を読む。EditModeInPlayingTest のドメインリロード後も維持
3. 自動プレイテスト（DSL）が開発者の実 cache を汚さない
4. クラッシュ・強制終了しても残置事故が起きない（後始末に依存しない）
5. テスト・プレイテストがデバッグ設定を必要とする場合、専用環境に明示的に用意できる

## 非ゴール

- 手動プレイ・手動 DebugSheet トグルの永続化は現状維持（意図された挙動）
- `DebugParameters` の DI 化はしない（client 側の静的利用箇所全域に波及する割に
  隔離目的への追加利益がないため不採用）
- Codex 監査で見つかったプレイテストシナリオの削除 API 参照修正は別件

## 設計

### 機構: キャッシュディレクトリのプロセス環境変数切替

`DebugParameters` のキャッシュディレクトリ解決を次に変更する:

```
環境変数 MOORESTECH_DEBUG_CACHE_DIR が設定されていればそのパス、
未設定なら従来の Path.GetFullPath("../cache")
```

- 解決は**アクセス毎**に行う（現状も `Load()` が毎アクセス走るため一貫）。
  `private static readonly CachePath` のクラス初期化時キャッシュは廃止する。
  静的初期化のタイミングが環境変数設定より早いと切替が効かないため
- プロセス環境変数を選ぶ理由:
  - ドメインリロードを生き延びる（Mono AppDomain は再生成されるがプロセスは同一）
    → EditModeInPlayingTest の PlayMode 遷移後・PlayMode 中のサーバー起動も隔離が維持される
  - エディタクラッシュ時はプロセスと共に消える → 残置事故が構造的に発生しない
  - `Common.Debug` はランタイムアセンブリのため `SessionState`（UnityEditor 専用）は使えない

### テスト側: アセンブリ単位の SetUpFixture

対象: `Server.Tests`（moorestech_server/Assets/Scripts/Tests）と
`Client.Tests`（moorestech_client/Assets/Scripts/Client.Tests）の2アセンブリ。

各アセンブリに **namespace 無し**（= アセンブリ全体に適用）の `[SetUpFixture]` を1ファイル新設:

- `[OneTimeSetUp]`: OS 一時領域に空のユニークディレクトリを作成し
  `MOORESTECH_DEBUG_CACHE_DIR` に設定
- `[OneTimeTearDown]`: 環境変数を解除（`SetEnvironmentVariable(name, null)`）し、
  一時ディレクトリを削除

挙動:

- 全テストがデバッグ設定**デフォルト値**で開始する（空環境）
- テストがデバッグ挙動自体を検証したい場合は `SaveBool` 等で隔離環境内に書ける
  （ファイルI/Oも隔離パスで実際に動くため、永続化ロジック自体も検証可能）
- クラッシュ時は OS 一時領域に孤児ディレクトリが残るのみ（実害なし・OS が回収）

制約（許容）: EditModeInPlayingTest がドメインリロードを跨いでも環境変数は残るが、
`[OneTimeTearDown]` 自体が走らない異常終了ではプロセス終了まで環境変数が残る。
その間に手動プレイは通常起きず、エディタ再起動で消えるため許容する。

### プレイテストDSL側: セッション専用環境への切替（復元機構の置換）

`PlaytestBoot.PrepareAndEnterPlayMode`:

1. `PlaytestPaths.ResetSession()` 後、セッションディレクトリ配下に `debug-cache/` を作成
2. 実 cache（`../cache`）の `{Bool,Int,String}DebugParameters.json` を存在するものだけコピー
   （**コピー継承**: 開発者の `WebUiCefActive=true` 等を引き継ぎ、現行のプレイテスト挙動を維持）
3. `MOORESTECH_DEBUG_CACHE_DIR` にそのパスを設定
4. 以降の同メソッド内の `DebugParameters` 書き込み（serverDirectory 上書き等）は
   必ず環境変数設定**後**に行う（隔離側に落とすため）

`OnPlayModeStateChanged(EnteredEditMode)`:

- 環境変数を解除する

これに伴い **DebugServerDirectory の退避・復元機構を削除**する:

- `PriorServerDirExistedKey` / `PriorServerDirKey` / `OverroteServerDirKey` の SessionState 3キーと
  復元ブロック（4f919ee70 で導入）
- serverDirectory の上書きは隔離環境への書き込みになるため、復元自体が不要になる
- 同等の保護がクラッシュ耐性つきで得られる（環境変数はプロセスと共に消える）

`PlaytestSetup.SetupDebugEnvironment` の `FreeBlockPlacement` 書き込みは変更不要
（PlayMode 中の実行時点で環境変数が有効なため、自然に隔離側へ書かれる）。

### 影響を受けない箇所（意図的に現状維持）

- `Editor/PlayModeSetting.cs` の serverDirectory 手動設定: 環境変数未設定の文脈で実行される
  ため実 cache に永続（開発者の明示操作なので意図通り）
- 手動 DebugSheet トグル・`WebUiCefToggle`: 手動プレイ中は環境変数未設定のため実 cache に永続

## エッジケース・自己反証

| ケース | 挙動 |
|---|---|
| テスト実行中にエディタクラッシュ | 環境変数はプロセスと共に消滅。実 cache 無傷。OS 一時領域に孤児ディレクトリのみ |
| プレイテスト中にエディタクラッシュ | 同上。従来は復元漏れで serverDirectory / FreeBlockPlacement が残置していた事故クラスが消滅 |
| プレイテスト連続実行 | 毎回新セッションの debug-cache を作り環境変数を上書き。前回の残骸を読まない |
| デバッグフラグ前提のテストを将来書く場合 | 空環境スタートのため明示 seed（SaveBool）が必要。暗黙依存を許さない意図的な厳格化 |
| EditModeInPlayingTest のドメインリロード | プロセス環境変数のため隔離が維持される |
| テストとプレイテストの同時実行 | 同一エディタでは構造上起きない（PlayMode 排他） |

## テスト戦略

- 既存の `ElectricWireAutoConnectPlaceTest` 等が、実 cache に `FreeBlockPlacement: true` を
  書いた状態でも成功することを手元で確認する（隔離の実証）
- `DebugParameters` の環境変数切替の単体テスト: 環境変数設定 → SaveBool → 隔離パスに
  ファイルが生成され実 cache が不変であること、解除後は実 cache 解決に戻ること
- プレイテストDSL: 既存シナリオを1本実行し、実 cache のタイムスタンプ・内容が
  実行前後で不変であることを確認する

## 実装対象ファイル

| ファイル | 変更 |
|---|---|
| `moorestech_server/Assets/Scripts/Common.Debug/DebugParameter.cs` | キャッシュディレクトリ解決を環境変数優先・毎アクセス解決に変更 |
| `moorestech_server/Assets/Scripts/Tests/DebugParametersTestIsolationFixture.cs` | 新規: namespace 無し SetUpFixture |
| `moorestech_client/Assets/Scripts/Client.Tests/DebugParametersTestIsolationFixture.cs` | 新規: 同上（Client.Tests 用） |
| `moorestech_client/Assets/Scripts/Client.Playtest/PlaytestBoot.cs` | セッション debug-cache 作成・コピー・環境変数設定/解除。DebugServerDirectory 退避・復元機構の削除 |

検証済み情報の範囲: 本設計時点で確認したのは「漏洩経路の特定（cache ファイル実物・
PlaytestSetup/PlaytestBoot のコード）」「キー除去後に対象222テストが成功」まで。
環境変数がドメインリロードを跨いで維持されることは Unity のプロセスモデルからの推論であり、
実装時に EditModeInPlayingTest 系で実測確認すること。
