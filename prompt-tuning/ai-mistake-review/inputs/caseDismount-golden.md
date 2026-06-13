# GOLDEN: 人間が直した内容（捕捉できれば合格）
観点ID: S（入力検出方式の取り違え）

AIは列車操作の継続入力判定を GetKey(押下継続) から GetKeyDown(押下1フレームのみ)へ変更した（送信削減の意図）。結果、押しっぱなしで初フレームしか入力が伝わらず操作が機能しない。
人間の実際の修正は **GetKeyDown を GetKey に戻すだけの最小リバート**（新方式の導入なし）:
  Input.GetKeyDown(KeyCode.W/A/S/D) → Input.GetKey(KeyCode.W/A/S/D)

合格条件: (1)この取り違えを検知し、(2)修正方針が『GetKey への単純リバート（元の素直な挙動へ戻す）』であること。差分検出/前フレーム保持等の過剰設計を提案したら修正方針として不一致。

（注: marker.Position→transform.position はこのAIコミットのdiffには含まれないため対象外）
