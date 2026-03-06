# テスト用コーディング規約

1. **コメントは日英2行セット**:
   ```csharp
   // チェストブロックの配置
   // Place the chest block
   ```

2. **#region Internal + ローカル関数**: 複雑なテストではヘルパーメソッドをローカル関数として定義
   ```csharp
   #region Internal
   void HelperMethod() { }
   #endregion
   ```

3. **try-catch使用禁止**

4. **デフォルト引数使用禁止**

5. **テスト用IDは必ず `ForUnitTestModBlockId`/`ForUnitTestItemId` から取得**

6. **テスト名は `{動作内容}Test` 形式**

7. **namespace規約**:
   - UnitTest: `Tests.UnitTest.{Layer}.{Category}`
   - CombinedTest: `Tests.CombinedTest.{Layer}`
   - PacketTest: `Tests.CombinedTest.Server.PacketTest`
