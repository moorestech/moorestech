import { Mermaid } from '../components/comment-feature';

const chart = `graph TD
    subgraph A["モジュール基盤レイヤー（全機械共通・軽い）"]
      SLOT["機械の N 個のモジュールスロット"]
      SPEED["速度: 処理時間を変える"]
      PROD["生産性: 産出を変える"]
      EFF["省エネ: 消費を変える"]
      SLOT --> SPEED
      SLOT --> PROD
      SLOT --> EFF
    end

    subgraph B["レベルファミリー機構（品質軸だけが依存・重い）"]
      QUAL["品質: 出力レベル分布を変える"]
      FAMILY["スキーマの levelFamily 定義"]
      GEN["SourceGenerator が Lv1..LvN と合成レシピを自動生成"]
      QUAL --> FAMILY
      FAMILY --> GEN
    end

    SLOT --> QUAL

    classDef found fill:#EFF6FF,stroke:#2563EB,stroke-width:2px,color:#0F172A
    classDef qual fill:#F5F3FF,stroke:#8B5CF6,stroke-width:2px,color:#0F172A
    class SLOT,SPEED,PROD,EFF found
    class QUAL,FAMILY,GEN qual

    linkStyle 5 stroke:#8B5CF6,stroke-width:2px,stroke-dasharray: 6 4`;

export function TwoLayer() {
  return (
    <section className="section" id="two-layer">
      <div className="eyebrow">05 — 2 レイヤー分離</div>
      <h2>「全般」の大部分は簡単。難所は品質軸 1 つだけ</h2>
      <p className="lead">
        4 つの効果軸は性質で 2 グループに分かれる。速度・生産性・省エネは機械処理を変えるだけで、
        アイテムの正体に触れない（軽い）。品質だけが「レベル違いアイテムの表現」を強制する（重い）。
      </p>

      <Mermaid
        id="two-layer-graph"
        chart={chart}
        label="図02 アップグレードシステムの2レイヤー構造。上の基盤レイヤーは機械のモジュールスロットから速度・生産性・省エネの3軸が伸び、機械処理だけを変える。品質軸（破線）だけが下のレベルファミリー機構（スキーマのlevelFamily定義→SourceGeneratorによるLv1〜LvNと合成レシピの自動生成）に依存する、という依存関係"
      />
      <div className="mermaid-legend">
        <span><span className="swatch" style={{ background: '#EFF6FF', border: '1px solid #2563EB' }} />基盤レイヤー（軽い・3軸）</span>
        <span><span className="swatch" style={{ background: '#F5F3FF', border: '1px solid #8B5CF6' }} />レベルファミリー機構（重い・品質のみ）</span>
      </div>

      <p>
        この分離が設計の核心。速度 / 生産性 / 省エネだけ使う機械は、レベルファミリー機構に<strong>一切依存しない</strong>。
        ただし<strong>完全分離ではない</strong>点に注意：品質モジュールは速度ペナルティを持つ（基盤の処理時間計算に入る）し、
        出力 ItemId を変える（出力容量判定に入る）。だから基盤の段階で、効果集計の統一結果に品質用フックの「場所」だけは確保しておく。
      </p>

      <div className="card accent-l">
        <h4>実装フェーズも分ける</h4>
        <p style={{ margin: 0 }}>
          <strong>フェーズA（基盤）</strong>＝速度・生産性・省エネが動く完結した増分。
          <strong>フェーズB（品質）</strong>＝レベルファミリー機構をAの確定APIの上に載せる。Aを先に動かせるのが利点。
        </p>
      </div>
    </section>
  );
}
