# Implementation Tasks

## タスク概要
本実装タスクは、歯車システムにおける過負荷破壊メカニズムを段階的に実装します。マスターデータスキーマの拡張から始まり、ブロック破壊インターフェースの定義、DIコンテナ統合、過負荷監視ロジックの実装、そして最終的なテストまでを含みます。

## タスクリスト

### 1. マスターデータスキーマとブロック破壊基盤の構築

- [ ] 1.1 (P) blocks.yamlに過負荷パラメータスキーマを追加
  - VanillaSchema/blocks.yamlを開き、defineInterfaceセクションにIGearOverloadParamを追加する
  - maxRpm（整数型、デフォルト: 0）、maxTorque（浮動小数点型、デフォルト: 0）、overloadCheckIntervalSeconds（浮動小数点型、デフォルト: 1.0）、baseBreakageProbability（浮動小数点型、デフォルト: 0.0）の4つのプロパティを定義する
  - 各プロパティに日本語と英語のコメントを記述する
  - _Requirements: 1.1, 1.2, 1.3, 1.4_

- [ ] 1.2 (P) SourceGeneratorによる自動生成を確認
  - Unityプロジェクトをリビルドし、SourceGeneratorが正常に動作することを確認する
  - Mooresmaster.Model.BlocksModule名前空間にIGearOverloadParamインターフェースが生成されていることを確認する
  - 型安全なプロパティアクセスが提供されていることをコード補完で確認する
  - _Requirements: 1.2, 1.3_

- [ ] 1.3 (P) BlockRemovalType enumを定義
  - Game.Block.Interface名前空間に新しいファイルBlockRemovalType.csを作成する
  - ManualRemove（手動削除）とBroken（システムによる破壊）の2つの値を持つenumを定義する
  - 各メンバーに日本語と英語のXMLドキュメントコメントを付与する
  - 将来的な拡張を考慮したコメントを追加する
  - _Requirements: 2.1, 2.2, 2.3, 2.4_

### 2. ブロック破壊インターフェースと実装の作成

- [ ] 2.1 (P) IBlockRemoverインターフェースを定義
  - Game.Block.Interface名前空間に新しいファイルIBlockRemover.csを作成する
  - RemoveBlock(BlockPositionInfo position, BlockRemovalType removalType)メソッドシグネチャを定義する
  - 各パラメータの意味を日本語と英語のXMLドキュメントコメントで明記する
  - インターフェース全体の責務を説明するコメントを追加する
  - _Requirements: 3.1, 3.2, 3.3, 3.4_

- [ ] 2.2 BlockRemoverクラスを実装
  - Game.Block名前空間に新しいファイルBlockRemover.csを作成する
  - IBlockRemoverインターフェースを実装し、IWorldBlockDatastoreへの依存をコンストラクタインジェクションで受け取る
  - RemoveBlock()メソッド内でworldDatastore.RemoveBlock(position.OriginPos)を呼び出す
  - 削除理由（removalType）をDebug.Log()で記録する
  - 日本語と英語のコメントをメソッド内の主要処理に付与する
  - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5_

### 3. DIコンテナ統合とBlockTemplate拡張

- [ ] 3.1 MoorestechServerDIContainerGeneratorにIBlockRemoverを登録
  - MoorestechServerDIContainerGenerator.csのCreate()メソッドを編集する
  - initializerCollectionセクションで、IWorldBlockDatastoreの登録後、VanillaIBlockTemplatesの登録前にIBlockRemoverをシングルトンとして登録する
  - initializerCollection.AddSingleton<IBlockRemover, BlockRemover>()を追加する
  - 登録順序が依存関係を満たすことを確認する
  - _Requirements: 5.1, 5.2, 5.3, 5.4_

- [ ] 3.2 VanillaIBlockTemplatesコンストラクタを拡張
  - VanillaIBlockTemplates.csのコンストラクタにIBlockRemoverパラメータを追加する
  - 受け取ったIBlockRemoverをプライベートフィールド_blockRemoverに保存する
  - 歯車関連のBlockTemplate生成時にIBlockRemoverを渡すよう修正する（Gear、Shaft、SimpleGearGenerator、FuelGearGenerator、GearElectricGenerator、GearMiner、GearMapObjectMiner、GearMachine、GearBeltConveyor）
  - 既存のIBlockOpenableInventoryUpdateEventパラメータとの互換性を保つ
  - _Requirements: 6.1, 6.2, 6.3, 6.4_

- [ ] 3.3 歯車関連BlockTemplateコンストラクタを拡張
  - VanillaGearTemplate、VanillaShaftTemplate、VanillaSimpleGearGeneratorTemplate、VanillaFuelGearGeneratorTemplate、VanillaGearElectricGeneratorTemplate、VanillaGearMinerTemplate、VanillaGearMapObjectMinerTemplate、VanillaGearMachineTemplate、VanillaGearBeltConveyorTemplateの各コンストラクタにIBlockRemoverパラメータを追加する
  - 各テンプレートのCreateBlock()メソッドでGearEnergyTransformerのコンストラクタにIBlockRemoverを渡すよう修正する
  - 他のブロックタイプ（ElectricMachine、Chest等）には影響しないことを確認する
  - _Requirements: 7.1, 7.2, 7.3, 7.4_

### 4. GearEnergyTransformer過負荷監視ロジックの実装

- [ ] 4.1 GearEnergyTransformerコンストラクタを拡張
  - GearEnergyTransformerComponent.csのコンストラクタにIBlockRemoverとGuid blockGuidパラメータを追加する
  - 受け取ったIBlockRemoverとblockGuidをプライベートフィールドに保存する
  - InitializeOverloadMonitoring()メソッドを呼び出し、過負荷監視処理を初期化する
  - 既存のコンストラクタ処理（GearNetworkDatastore.AddGear等）との整合性を保つ
  - _Requirements: 7.2, 8.1_

- [ ] 4.2 過負荷パラメータ読み込みロジックを実装
  - GearEnergyTransformer内にLoadOverloadParameters()ローカル関数を実装する
  - MasterHolder.BlockMaster.GetBlockMaster(blockGuid)でマスターデータを取得する
  - BlockParamをIGearOverloadParamにキャストし、過負荷パラメータを読み込む
  - パラメータが未定義の場合はmaxRpmとmaxTorqueを0に設定して過負荷チェックを無効化する
  - overloadCheckIntervalSecondsが0以下の場合はデフォルト値1.0を使用する
  - #region Internalでローカル関数を整理する
  - _Requirements: 8.1, 11.1, 11.2_

- [ ] 4.3 GameUpdater購読と定期チェック処理を実装
  - GearEnergyTransformer内にInitializeOverloadMonitoring()メソッドを実装する
  - 過負荷チェックが有効な場合（maxRpm > 0 && maxTorque > 0 && baseBreakageProbability > 0）のみGameUpdater.UpdateObservable.Subscribe()を実行する
  - OnUpdate()ローカル関数で経過時間を累積し、overloadCheckIntervalSecondsごとに過負荷チェックを実行する
  - GameUpdater.UpdateSecondTimeを使用して経過時間を計算する
  - _updateSubscriptionフィールドにIDisposableを保存し、Destroy()時にDispose()する
  - _Requirements: 8.2, 8.4, 8.5_

- [ ] 4.4 過負荷検出と確率計算ロジックを実装
  - OnUpdate()ローカル関数内でGearNetworkDatastore.GetGearNetwork(BlockInstanceId)を呼び出し、現在のネットワークを取得する
  - ネットワーク取得失敗時はreturnで処理をスキップする（条件分岐で対応）
  - CurrentRpmとCurrentTorqueをmaxRpmおよびmaxTorqueと比較し、過負荷を検出する
  - CalculateBreakageProbability()ローカル関数でRPM超過倍率、トルク超過倍率を計算し、最終破壊確率を算出する
  - 破壊確率をMathf.Clamp01()で0.0～1.0の範囲に制限する
  - #region Internalでローカル関数を整理する
  - _Requirements: 8.2, 8.3, 9.1, 9.2, 9.3, 9.4, 9.5, 9.6, 11.3, 11.4, 11.5_

- [ ] 4.5 確率的破壊実行ロジックを実装
  - OnUpdate()ローカル関数内でUnityEngine.Random.Range(0f, 1f)で乱数を生成する
  - 乱数が破壊確率以下の場合、IBlockRemover.RemoveBlock()を呼び出す
  - RemoveBlock()の引数としてBlockPositionInfo（ServerContext.WorldBlockDatastore経由で取得）とBlockRemovalType.Brokenを渡す
  - ブロック破壊後は_updateSubscription?.Dispose()で監視処理を停止する
  - 乱数が破壊確率を上回った場合は何もせず、次のチェック間隔まで待機する
  - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.5, 10.6_

- [ ] 4.6 Destroy()メソッドを拡張
  - GearEnergyTransformerのDestroy()メソッド内で_updateSubscription?.Dispose()を呼び出し、購読を解除する
  - 既存のDestroy()処理（GearNetworkDatastore.RemoveGear、_simpleGearService.Destroy等）との順序を確認する
  - 監視処理が確実に停止されることを確認する
  - _Requirements: 8.5_

### 5. コンパイルエラーの解消とマスターデータ追加

- [ ] 5.1 コンパイルエラーを確認・修正
  - MCPツール（mcp__moorestech_server__RefreshAssets）を使用してサーバー側をコンパイルする
  - GetCompileLogsでエラーを確認し、コンストラクタシグネチャ変更に伴うコンパイルエラーを修正する
  - 歯車関連BlockTemplateのCreateBlock()呼び出し箇所でblockGuidを渡すように修正する
  - すべてのコンパイルエラーが解消されるまで修正を繰り返す
  - _Requirements: 全要件（統合タスク）_

- [ ] 5.2 テスト用マスターデータに過負荷パラメータを追加
  - Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/blocks.jsonを編集する
  - テスト用歯車ブロックにIGearOverloadParamのパラメータを追加する（例：maxRpm: 100, maxTorque: 50.0, overloadCheckIntervalSeconds: 0.5, baseBreakageProbability: 0.1）
  - ForUnitTestModBlockId.csで使用されているブロックIDに対応するブロックに追加する
  - JSON形式が正しいことを確認する
  - _Requirements: 12.4_

### 6. ユニットテストの作成と実行

- [ ] 6.1 BlockRemoverのユニットテストを作成
  - Tests/UnitTest/Game/BlockRemoverTest.csを新規作成する
  - IWorldBlockDatastoreをモック化し、BlockRemoverのコンストラクタに注入する
  - RemoveBlock()呼び出し時にworldDatastore.RemoveBlock()が呼ばれることを検証する
  - RemoveBlock()の引数（position、removalType）が正しく渡されることを検証する
  - MCPツール（mcp__moorestech_server__RunEditModeTests）でテストを実行する
  - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5_

- [ ] 6.2 GearEnergyTransformer過負荷ロジックのユニットテストを作成
  - Tests/CombinedTest/Core/GearOverloadTest.csを新規作成する
  - IBlockRemoverをモック化し、GearEnergyTransformerのコンストラクタに注入する
  - テスト用マスターデータを使用してGearEnergyTransformerを初期化する
  - GameUpdater.SpecifiedDeltaTimeUpdate()を使用して時間を進め、過負荷チェックが実行されることを確認する
  - 閾値超過時にIBlockRemover.RemoveBlock()が呼ばれることを検証する
  - 異なる超過倍率（RPMのみ、トルクのみ、両方）における破壊確率計算を検証する
  - MCPツール（mcp__moorestech_server__RunEditModeTests）でテストを実行する
  - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5, 9.1, 9.2, 9.3, 9.4, 9.5, 9.6, 10.1, 10.2, 10.3, 10.4, 10.5, 10.6, 11.1, 11.2, 11.3, 11.4, 11.5, 12.1, 12.2, 12.3, 12.5_

### 7. 統合テストと最終検証

- [ ] 7.1 歯車ネットワーク統合テストを作成
  - Tests/CombinedTest/Core/GearNetworkOverloadIntegrationTest.csを新規作成する
  - 複数のGearEnergyTransformerを含む歯車ネットワークを構築する
  - 過負荷状態を意図的に作り出し、ブロック破壊が実行されることを確認する
  - 破壊後のGearNetworkDatastoreの状態（ネットワークから削除されていること）を検証する
  - 破壊されなかったブロックが正常に動作し続けることを確認する
  - MCPツール（mcp__moorestech_server__RunEditModeTests）でテストを実行する
  - _Requirements: 全要件（統合検証）_

- [ ] 7.2 境界条件とエラーハンドリングのテストを作成
  - GearOverloadTest.cs内に境界条件テストケースを追加する
  - maxRpmまたはmaxTorqueが0以下の場合、過負荷チェックが無効化されることを確認する
  - overloadCheckIntervalSecondsが0以下の場合、デフォルト値1.0が使用されることを確認する
  - baseBreakageProbabilityが0.0以下および1.0以上の場合、確率が適切にクランプされることを確認する
  - GearNetwork取得失敗時に例外が発生せず、スキップされることを確認する
  - MCPツール（mcp__moorestech_server__RunEditModeTests）でテストを実行する
  - _Requirements: 11.1, 11.2, 11.3, 11.4, 11.5, 11.6_

## タスク実行ガイドライン

### 並列実行可能なタスク (P)マーク付き
以下のタスクは独立しており、並列実行が可能です：
- タスク1.1、1.2、1.3（マスターデータスキーマ、SourceGenerator確認、BlockRemovalType定義）
- タスク2.1（IBlockRemover定義）

これらのタスクは異なるファイルを対象とし、データ依存関係がないため、同時に作業できます。

### 順次実行が必要なタスク
- タスク2.2以降は、前のタスクの成果物に依存するため、順次実行が必要です。
- タスク3.1、3.2、3.3はDIコンテナ統合のため、この順序で実行してください。
- タスク4.1～4.6はGearEnergyTransformerの拡張であり、ロジックが密結合しているため順次実行が推奨されます。
- タスク5.1のコンパイル確認は、すべての実装タスク完了後に実行してください。
- タスク6.1、6.2のユニットテストは、対応する実装完了後に実行してください。
- タスク7.1、7.2の統合テストは、すべての実装とユニットテストが完了した後に実行してください。

### テスト実行
- 各テストタスクでは、MCPツール（mcp__moorestech_server__RunEditModeTests）を使用して、特定のテストクラスのみを実行してください。
- groupNamesパラメータで実行対象を限定し、全テストの一括実行は避けてください。
- 例：`groupNames: ["^Tests\\.CombinedTest\\.Core\\.GearOverloadTest$"]`

### コンパイル確認
- タスク5.1では、MCPツール（mcp__moorestech_server__RefreshAssets）でコンパイルを実行し、mcp__moorestech_server__GetCompileLogsでエラーを確認してください。
- コンパイルエラーが発生した場合、エラーメッセージを分析し、該当箇所を修正してください。
