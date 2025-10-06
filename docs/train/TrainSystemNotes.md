# Train System Notes

列車システムの実装・テスト方針を以下にまとめます。テストや仕様確認の際は [Train Documentation Index](README.md) と併せて参照してください。

## テスト環境のセットアップ
- `TrainTestHelper.CreateEnvironment()` を利用すると、レールグラフや依存関係が初期化された決定論的なテスト環境を即座に構築できます。
- レールや駅ブロックは `TrainTestHelper.PlaceRail(...)` などのユーティリティと `Tests.Module.TestMod.ForUnitTestModBlockId` に定義されたブロックIDを使って配置します。
- シミュレーションを進める場合は、ヘルパーの更新メソッドで固定時間ステップを刻み、無限ループ防止のガード条件を明示します。
- セーブ/ロードや複数環境を跨いだ検証を行うときは、`RailGraphDatastore.ResetInstance()` などのシングルトンを必ずリセットしてください。

## ドッキングとハンドル参照
- ドッキング挙動の検証では `DockingHandle` や `TrainDockingHandle` といったハンドル参照を信頼ソースにします。`TrainUnit` のローカルキャッシュだけに依存しないことで、状態同期ずれを防ぎます。
- `docs/train/TrainTickSimulation.md` の挙動保証と突き合わせ、Tick駆動の処理やスケジューリングを変更する際はドキュメントを更新してください。

## RailComponent の front/back モデル
- 各 `RailComponent` はコンストラクタ内で `FrontNode` と `BackNode` の 2 つの `RailNode` を生成し、互いを `OppositeNode` として登録します。これらはベジェ曲線の制御点とともに `RailGraphDatastore` に登録され、片方向グラフのノードとして扱われます。
- コンポーネント同士を接続する際は、接続元側で選んだノード (`FrontNode` もしくは `BackNode`) から相手側で選んだノードへ有向エッジが張られます。同時に、逆方向用のエッジは相手コンポーネントの *反対側* ノードから自分の反対側ノードへ接続されます。例えば `front(A) -> front(B)` で接続すると、逆方向は `back(B) -> back(A)` という別経路として登録されます。
- この振る舞いにより、前進方向と後退方向はグラフ上で別ノードとして扱われ、距離計算や経路探索 (`RailNode.ConnectedNodes` / `FindShortestPath`) では明示的に片方向エッジを辿ります。従って、双方向を取り扱う処理では `FrontNode` / `BackNode` の両方を参照し、適切に逆側ノードを指定してください。
- **重要**: RailNode レベルでは常に「有向グラフ」として設計されています。`ConnectNode` を相互に呼び出して無理に双方向へ張り直すと、front/back の対応が崩れて駅構内の距離計算が破綻します。方向が必要な場合は、必ず既存の opposite ノード経路を利用してください。
- 接続解除や距離計算のヘルパー (`DisconnectRailComponent`, `GetDistanceToNode` など) も front/back を引数で受け取り、誤って表裏を取り違えると逆方向のエッジが残るため注意が必要です。

## 用語整理
- **Front / Back**: レール片のジオメトリ上の「前後」ではなく、グラフ上での進行方向を示す補助的な名前です。レールの向きが変わっても `FrontNode` は常に「このコンポーネントを前進したときに辿るノード」として扱われます。
- **OppositeNode**: 同一 `RailComponent` の裏表ペア。逆方向経路を確立する際に使用します。
- **ConnectionDestination**: ノードと `RailComponentID` の対応をセーブ/ロード用に管理する仕組みです。front/back を明示的に識別するため、ドキュメント化された表記 (`front`, `back`) を保持しています。

これらのルールを守ることで、front/back を巡る接続の不整合や、逆方向経路の欠落を防止できます。レール構造を変更する場合は、本資料を更新してから他の仕様書にも反映してください。
