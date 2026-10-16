# ベルト搬送のsegment生成と出力配線はサーバー組立器のstaticに一本化する

日付: 2026-10-14
文脈: サーバーの BeltTransportAssembler とクライアント複製の BeltTransportReplicaAssembler が segment の生成・出力配線・容量規則を別々に持っていた。片側だけ変えると複製の構成がずれ、ハッシュ不一致で全クライアントが恒久停止する（moores-code-review C7 / D1）。

決定: 既存 BeltTransportAssembler に static の CreateSegment と ConnectOutput を置き、両組立器がそれを呼ぶ。容量は BeltSegmentShape が FromLayout 時に値で保持し、式を1箇所にする。新設型は作らない。出所: ユーザー裁定 2026-10-14「案A: 既存 Assembler に static 2本（推奨）」
棄却案: BeltSegmentFactory / BeltSegmentOutputWiring を static クラスで新設（未知 Kind を例外にできるが型が2本増える）。組立手順全体を1メソッドへ寄せ機械受け手だけ IBeltMachineReceiverProvider で差し替える（共通入力型の導入が要り変更量が最大）。
理由: 新設型0で生成・配線・容量の式が1箇所になり、症状（複製の構成ずれ）が消える最小の形。
リンク: moorestech_logs/harness/moores-code-review/runs/2026-10-13-2340-belt-segment-client-sync-22de6217/design.md
