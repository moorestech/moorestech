import { Mermaid } from '../components/comment-feature';

const chart = `sequenceDiagram
    participant Slot as モジュールスロット
    participant Proc as 機械プロセッサ
    participant Out as 出力インベントリ

    rect rgba(37, 99, 235, 0.08)
      Note over Proc: 処理開始（Idle → Processing）
      Slot->>Proc: 装着モジュールを集計
      Proc->>Proc: 効果をスナップショット（処理中は固定）
      Proc->>Proc: 処理時間を clamp（最低1tick）
      Proc->>Out: 追加産出の最大分まで仮想容量で予約
      Out-->>Proc: 入らなければ開始しない
    end

    rect rgba(5, 150, 105, 0.08)
      Note over Proc: 完了時
      Proc->>Out: 基本レシピ出力を格納
      Proc->>Proc: 決定的乱数で追加産出を判定
      Proc->>Out: 当たれば「アイテム出力のみ」1セット追加
      Proc->>Proc: 完了回数をインクリメント（セーブ対象）
    end`;

export function ProcessFlow() {
  return (
    <section className="section" id="invariants">
      <div className="eyebrow">06 — 設計を壊しかけた不変条件</div>
      <h2>「動く実装」になるかは、ここで決まる</h2>
      <p className="lead">
        効果を機械処理に適用する手順には、外部監査（Codex）が実コードを読んで指摘した地雷が並ぶ。
        これらはバランス調整ではなく<strong>不変条件</strong>の問題で、曖昧なまま実装するとバグになる。
      </p>

      <Mermaid
        id="process-flow"
        chart={chart}
        label="図03 機械がモジュール効果を適用する手順。処理開始時にモジュールを集計して効果をスナップショット固定し、処理時間を最低1tickにclampし、追加産出の最大分まで出力の仮想容量で予約してから開始する。完了時は基本出力を格納し、決定的乱数で追加産出を判定して当たればアイテム出力のみ1セット追加し、完了回数をセーブ対象としてインクリメントする流れ"
      />
      <div className="mermaid-legend">
        <span><span className="swatch" style={{ background: 'rgba(37,99,235,0.18)' }} />処理開始フェーズ</span>
        <span><span className="swatch" style={{ background: 'rgba(5,150,105,0.18)' }} />完了フェーズ</span>
      </div>

      <h3>監査で潰した 5 つの地雷</h3>
      <ul className="checks">
        <li><span className="mk no">1</span><span><strong>加算は必ず破綻する</strong> — 単純合算だと処理時間0以下・確率100%超・負確率が出る。最終段で必ず clamp（ticks≥1 / 消費≥下限 / 確率∈[0,1] / 分布和=1）。</span></li>
        <li><span className="mk no">2</span><span><strong>処理中の抜き差し</strong> — 効果を開始時にスナップショットしないと「開始だけ速度盛り→直前だけ品質盛り」の exploit。さらにスナップショットを<strong>セーブにも永続化</strong>しないと、既存ロードがベース時間から再計算して進捗が狂う。</span></li>
        <li><span className="mk no">3</span><span><strong>追加産出の二重格納</strong> — 既存 InsertOutputSlot を2回呼ぶと液体まで倍増。追加産出は「アイテム出力のみ1セット」の専用APIにする。</span></li>
        <li><span className="mk no">4</span><span><strong>出力容量の食い合い</strong> — 既存判定は各出力を独立OR判定で複数出力の競合を無視。仮想スロットへ順次挿入してシミュレートする。</span></li>
        <li><span className="mk no">5</span><span><strong>非決定的な乱数</strong> — 全機械共有の static Random は順序・ロード依存。blockInstanceId と永続化した完了回数から導出する決定的乱数を使う。</span></li>
      </ul>

      <h3>決定的乱数（保存される状態だけから導出）</h3>
      <div className="code">{`// blockInstanceId と完了回数から決定的に [0,1) を返す
`}<span className="c">{`// （セーブ/ロード・ブロック更新順に非依存）`}</span>{`
`}<span className="k">private static</span>{` `}<span className="t">double</span>{` `}<span className="fn">DeterministicRoll</span>{`(BlockInstanceId id, `}<span className="t">int</span>{` cycleCount)
{
    `}<span className="t">ulong</span>{` x = (`}<span className="t">ulong</span>{`)id.AsPrimitive() * `}<span className="n">0x9E3779B97F4A7C15</span>{`UL + (`}<span className="t">ulong</span>{`)(`}<span className="t">uint</span>{`)cycleCount;
    x ^= x >> `}<span className="n">30</span>{`; x *= `}<span className="n">0xBF58476D1CE4E5B9</span>{`UL;
    x ^= x >> `}<span className="n">27</span>{`; x *= `}<span className="n">0x94D049BB133111EB</span>{`UL;
    x ^= x >> `}<span className="n">31</span>{`;
    `}<span className="k">return</span>{` (x >> `}<span className="n">11</span>{`) * (`}<span className="n">1.0</span>{` / (`}<span className="n">1</span>{`UL << `}<span className="n">53</span>{`));
}`}</div>
    </section>
  );
}
