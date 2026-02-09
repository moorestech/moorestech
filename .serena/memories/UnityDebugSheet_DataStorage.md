# UnityDebugSheet データストレージシステム調査結果

## 概要
moorestech クライアント側のUnityDebugSheetは、デバッグシート上の各種設定値をファイルベースで永続化しています。すべてのデータはプロジェクトルート相対で `cache/` ディレクトリに JSON形式で保存されています。

## データ保存先

### ファイルパス
- **プロジェクトルート**: `/Users/katsumi/moorestech/`
- **キャッシュディレクトリ**: `/Users/katsumi/moorestech/cache/`

### 保存ファイル構成
1. **BoolDebugParameters.json** - ブール型パラメータ
   - 現状: `{"keys":[],"values":[]}`
   - 例: IsItemListViewForceShow, SkitPlaySettings など

2. **IntDebugParameters.json** - 整数型パラメータ
   - 現状: `{"keys":["DebugEnvironmentTypeKey","FpsLimit"],"values":[2,30]}`
   - 例: FPS制限設定（現在30）、環境選択（2=Debug環境）

3. **StringDebugParameters.json** - 文字列型パラメータ
   - 現状: `{"keys":["DebugServerDirectory"],"values":["/Users/katsumi/moorestech_master/server_v5"]}`
   - 例: デバッグサーバーディレクトリパス

## 実装詳細

### コア実装クラス

#### DebugParameters (moorestech_server側)
位置: `/moorestech_server/Assets/Scripts/Common.Debug/DebugParameter.cs`

主な機能:
- `SaveBool(key, value)` - ブール値を保存
- `SaveInt(key, value)` - 整数値を保存
- `SaveString(key, value)` - 文字列値を保存
- `GetValueOrDefaultBool(key, defaultValue)` - ブール値を取得
- `GetValueOrDefaultInt(key, defaultValue)` - 整数値を取得
- `GetValueOrDefaultString(key, defaultValue)` - 文字列値を取得

実装方法:
- メモリ上の Dictionary にデータを保持
- Save/Load時に JSON シリアライズ
- SerializableDictionary クラスで Dictionary → JSON 変換
- キャッシュディレクトリ作成は自動（EnsureCacheDirectoryExists）

#### DebugSheetControllerExtension
位置: `/moorestech_client/Assets/Scripts/Client.DebugSystem/DebugSheet/DebugSheetControllerExtension.cs`

拡張メソッド:
- `AddBoolWithSave()` - ブール値トグルを追加（自動保存）
- `AddEnumPickerWithSave()` - Enum選択肢を追加（自動保存）

保存フロー:
1. UI値が変更されると、AddBoolWithSave/AddEnumPickerWithSave の valueChanged コールバック実行
2. DebugParameters.SaveBool/SaveInt を呼び出し
3. ディスク上の JSON ファイルに即座に反映

## デバッグシート設定一覧

### DebugConst.cs で定義されているキー
位置: `/moorestech_client/Assets/Scripts/Client.Common/DebugConst.cs`

| Label | Key | Type | 説明 |
|-------|-----|------|------|
| Item list view force show | IsItemListViewForceShow | Bool | アイテムリストビューの強制表示 |
| Skit play setting | SkitPlaySettings | Bool | スキットプレイ設定 |
| Map object super mine | MapObjectSuperMine | Bool | マップオブジェクト超採掘モード |
| Fix fast craft time | FixCraftTime | Bool | クラフト時間固定 |
| Train auto run | TrainAutoRun | Bool | 列車自動走行 |
| Place preview keep (no send) | PlacePreviewKeep | Bool | 配置プレビュー保持 |
| FPS Limit | FpsLimit | Int | FPS上限（enum: 10,20,30,60,120,144） |
| Select Environment | DebugEnvironmentTypeKey | Int | デバッグ環境選択 |
| Debug Server Directory | DebugServerDirectory | String | デバッグサーバーディレクトリ |

## UI実装体系

### DebugSheetController.cs
位置: `/moorestech_client/Assets/Scripts/Client.DebugSystem/DebugSheet/DebugSheetController.cs`

初期化フロー:
1. RuntimeInitializeOnLoadMethodで「moorestech Debug Objects」プリファブをロード
2. DebugSheet.GetOrCreateInitialPage()で初期ページ取得
3. AddBoolWithSave/AddEnumPickerWithSaveで設定項目追加

### カスタムDebugSheet実装例

1. **ItemGetDebugSheet** - アイテム取得デバッグページ
2. **SkitDebugSheet** - スキット再生デバッグページ
3. **CinematicCameraDebugSheet** - シネマティックカメラデバッグページ

## JSON構造体

### SerializableDictionary
- Unity の JsonUtility で Dictionary をシリアライズ可能にするラッパークラス
- keys: キーのリスト
- values: 値のリスト
- ISerializationCallbackReceiver で serialize/deserialize 時の変換を実装

## ファイル読み書きの仕組み

### Save メソッド
```csharp
1. EnsureCacheDirectoryExists() - ディレクトリ作成
2. Dictionary → SerializableDictionary に変換
3. JsonUtility.ToJson() で JSON 文字列に変換
4. File.WriteAllText() でファイルに書き込み
```

### Load メソッド
```csharp
1. File.Exists() でファイル存在確認
2. File.ReadAllText() でファイル読み込み
3. JsonUtility.FromJson() で Dictionary に変換
4. メモリ上の Dictionary に格納
```

## 重要な特性

1. **プロジェクトルート相対パス**: `../cache` で指定（相対パスを使用）
2. **自動作成**: キャッシュディレクトリは存在しなければ自動作成
3. **即座反映**: Save/Load が呼ばれるたびにディスク I/O
4. **キャッシュメカニズムなし**: 毎回ファイルから読み込み、毎回ファイルに書き込み
5. **複数プロセス対応**: ファイルロック機構なし（読み書き時の競合に注意）
