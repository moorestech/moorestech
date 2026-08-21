# スキット中断時の世界表示はSkitManagerのCleanupで戻す

決定: `SkitWorldObjectControlGroup` が「隠した事実」(`IsHidden`)だけを控え、`SkitManager.Cleanup()` が中断時に `SetActive(true)` を流して復元する。

棄却案:
- interface契約を `SetActive(bool)` から `BeginSkitHide/EndSkitHide`（実装側が直前の activeSelf を保持）へ変更 — 元から非アクティブだったものまで正しく戻せるが、兄弟3 interface（背景・ブロック・エンティティ）との非対称が生まれるか、それらへの横展開で波及が大きい
- 非目標として据え置き — レビューが「planにしか書かれていない非目標は免責力を持たない」と判定したため、据え置くならADR実体への追記が必要だった

理由: pinの「実際に効いたものだけ戻す」形に寄せられ、interface変更を伴わないため波及が最小。元から非アクティブだったものを復帰時に表示してしまうケースは許容コストとして残す。

リンク: [[2026-08-18-スキットの世界非表示は共通interfaceへ載せ替える.md]] / docs/adr/0016-skit-hides-world-objects-through-shared-interface.md
