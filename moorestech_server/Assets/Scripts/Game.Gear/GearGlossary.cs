namespace Game.Gear
{
    /// <summary>
    /// 歯車動力システムの用語集。別エージェント・開発者がコードから設計語彙を一箇所で読めるよう集約する。
    /// 各語は実装クラスと対応づけてあるので、まずここを読んでから個別クラスへ辿ること。
    /// Glossary of the gear power system, centralized so agents/developers can read the design vocabulary from code.
    /// Each term links to its implementing type; read this first, then follow into the concrete classes.
    ///
    /// 【ネットワーク / ギアネットワーク / 歯車ネットワーク（すべて同義）】 Network (a.k.a. gear network)
    /// 噛み合い・チェーンでつながった歯車の1つの連結グループ（グラフの連結成分）。つながっていなければ別ネットワーク。
    /// 実体は <see cref="Common.GearNetwork"/>。所属関係の管理は <see cref="Topology.GearNetworkTopologyMap"/>。
    /// One connected group (connected component) of meshed/chained gears; disconnected gears form separate networks.
    ///
    /// 【原点 / 原点generator】 Origin / origin generator
    /// そのネットワークでRPMの基準となる generator。ネットワーク内で最も速い（GenerateRpm が最大の）generator を採用する。
    /// キャッシュ上の識別子は <see cref="Tick.GearNetworkRotationCache.OriginBlockInstanceId"/>。
    /// The generator that serves as the RPM reference for the network; the fastest generator (max GenerateRpm) is chosen.
    ///
    /// 【符号付き原点RPM比】 Signed origin RPM ratio
    /// 各 gear が原点の RPM に対して持つ符号付き float 一本の比（<see cref="Tick.GearRotationRatio"/>）。原点自身は +1。
    /// 噛み合いで反転するたびに符号が反転し（逆向き＝負値）、歯車同士の噛み合いでは歯数比で大きさがスケールする。
    /// 実RPM = |比| × 原点RPM。絶対回転方向 = 比の符号を原点generatorの GenerateIsClockwise で解釈する（正=原点と同じ向き）。
    /// 同一 gear に異なる符号の比が伝播したら逆回転衝突（恒久ロック）、同符号で大きさが異なれば歯数比矛盾
    /// （|大きさ差| × 原点RPM が閾値超過で毎tick評価）で、いずれもネットワーク停止(Rocked)。
    /// A single signed float per gear (GearRotationRatio); origin = +1. The sign flips on each meshing reversal (negative = reverse),
    /// and gear-to-gear meshes scale the magnitude by the teeth ratio. Actual RPM = |ratio| × origin RPM; the absolute direction is
    /// the sign interpreted via the origin's GenerateIsClockwise. A sign conflict is a permanent reverse-rotation lock; same-sign
    /// magnitude conflicts are evaluated per tick against the origin RPM. Either stops the network (Rocked).
    ///
    /// 【導出（gear単位の現在値は保持しない）】 Derivation (no per-gear stored values)
    /// 各 gear の実RPM/実トルク/絶対向きは保存せず、符号付き原点RPM比 × 原点RPM と network単位stateから読むたびに O(1) 導出する。
    /// 入口は <see cref="Common.GearNetwork.TryResolveRotation"/>。保持するのは traversal cache（比＋原点参照）と
    /// network単位state（<see cref="Tick.GearRuntimeStateStore"/>：停止/理由/需給/負荷率）だけ。
    /// Per-gear RPM/torque/direction are never stored; they are derived O(1) on read from the signed ratio × origin RPM and the
    /// per-network state. The only held data are the traversal cache (ratios + origin reference) and the per-network state.
    ///
    /// 【需要snapshot】 Demand snapshot
    /// その tick に各 consumer が要求する動力をまとめた入力（<see cref="Tick.GearDemandSnapshot"/>）。
    /// GearNetwork は必ずこれ経由で需要を決め、consumer へ直接問い合わせない。現状は全 consumer 固定需要（有効・割合1）。
    /// Input aggregating each consumer's demand for the tick; GearNetwork derives demand only through it (currently fixed full demand).
    ///
    /// 【all-or-nothing / blackout】 All-or-nothing / blackout
    /// demandPower ≤ availablePower なら全 gear が要求トルク満額、超えたらネットワーク全体を停止（OverRequirePower）。部分供給は存在しない。
    /// If demand ≤ available every gear gets full torque; otherwise the whole network stops (OverRequirePower). No partial supply exists.
    ///
    /// 【active-set】 Active set
    /// 需給再計算は「topology変更 or generator出力変化通知があった network」だけに行う。安定tickは再計算0・通知0で全走査しない。
    /// Supply-demand recalculation runs only on networks with a topology change or a generator output-change notification;
    /// stable ticks recalculate nothing and notify nothing.
    ///
    /// 【トポロジー】 Topology
    /// ネットワークのつながり方の構造。歯車の追加/削除でのみ変化する。原点RPM比・回転方向はトポロジーと歯数だけで決まるため、
    /// トポロジーが変わらない限り再計算不要。変更時のみ全 gear の原点RPM比を再構築する（<see cref="Tick.GearRotationTraversalBuilder"/>）。
    /// The connectivity structure of a network; changes only on gear add/remove. Ratios and directions depend solely on
    /// topology and teeth counts, so they are recomputed only when topology changes.
    ///
    /// 【tick】 Tick
    /// サーバのシミュレーション1コマ（1秒 = 20 tick）。gear 関連 tick の唯一の入口は <see cref="Common.GearTickUpdater"/>。
    /// One server simulation step (1 second = 20 ticks). The sole entry point of gear ticks is GearTickUpdater.
    /// </summary>
    public static class GearGlossary
    {
    }
}
