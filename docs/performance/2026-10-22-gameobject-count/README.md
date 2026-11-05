# GameObject 数の内訳（2026-10-22）

ベースラインワールド（`2026-10-22-world_1`）を読み込んで定常状態になったあと、シーン内の全 Transform を数えた（`go-breakdown.cs`、生データは [breakdown-raw.txt](breakdown-raw.txt)）。

## 数字
| 項目 | 数 |
|---|---:|
| Transform（非アクティブ含む） | **982,757** |
| うちアクティブ | 308,134 |
| 設置ブロック | 15,082 |
| ブロック配下の GameObject | **901,139**（1ブロックあたり平均約60個） |
| ブロック配下の Renderer / Collider | 410,870 / 163,158 |
| MeshRenderer（全体） | 444,286 |
| 有効な Renderer のうち、画面に映っているもの（`isVisible`） | **7,401 / 207,058（約3.6%）** |
| MonoBehaviour（全体） | 359,277 |

## GameObject の多いブロック（上位）
| ブロック | GameObject | Renderer | Collider |
|---|---:|---:|---:|
| 鉄の歯車ベルトコンベア | 267,264 | 115,200 | 55,296 |
| 基本土台 | 148,896 | 85,728 | 15,792 |
| 直線歯車ベルトコンベア | 147,573 | 64,725 | 28,479 |
| 木のシャフト | 70,272 | 21,960 | 10,248 |
| 鋼鉄のパイプ | 51,254 | 21,966 | 8,368 |

ベルトコンベア系と土台だけで、ブロック配下の約6割を占める。

## 数の多い MonoBehaviour
| 型 | 数 | 備考 |
|---|---:|---|
| `BlockPreviewBoundingBoxSurface` | 90,492 | 設置プレビュー用のバウンディングボックス。全設置ブロックに生成している（`BlockGameObject.Initialize` → `BlockPreviewBoundingBoxLoader`）。通常は非表示 |
| `BlockGameObjectChild` | 67,077 | |
| `PreviewOnlyObject` | 32,509 | プレビュー時だけ使う部品。通常は非表示 |
| `GearStateChangeProcessorSimulator` / `GearStateChangeProcessor` | 12,337 / 12,103 | |

## 気になる点
- `BlockIconImagePhotographer` の下に、ブループリント「工場」のサムネイル用被写体（`BlueprintThumbnailSubject`）が **37,522 GameObject** 残っていた。計測時に撮影中だった1件なのか、撮影の失敗で `Destroy` されずに残ったものかは未確認。`TryTakeSubjectIconImage` は撮影が例外で抜けると被写体を破棄しない作りで、Editor.log には `BlockIconCaptureShot` のエラーが複数回出ていた。

## 負荷への影響（低fps調査の Profiler 値から）
定常時は、カメラ描画 約10ms、Renderer・Transform 更新 約4〜5ms、物理 約2.5〜3.5ms、SkinnedMesh 約2.7ms、`GearStateChangeProcessor.Update` 約2.7ms で、合計約20〜25ms/フレーム。読み込み時は、ブロック生成の1フレームが約12秒（`Instantiate` 15,082回で 3.8秒、残りは生成後の初期化）。
