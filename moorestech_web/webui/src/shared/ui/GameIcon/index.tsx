import { useEffect, useState } from "react";
import { useConnectionStatus, type ConnectionStatus } from "@/bridge";
import { L, useI18n } from "@/shared/i18n";
import styles from "./style.module.css";

// fallbackは「未指定」をundefinedセンチネルで表さず判別リテラルにする
// fallback is a discriminated literal, not an undefined sentinel for "unspecified"
export type IconFallback = { kind: "idText" } | { kind: "label"; text: string } | { kind: "none" };

// 実際に何を描いたか。呼び出し側の「中身あり」判定を推測ではなく描画結果から導かせる
// What actually got drawn, so a caller derives "has content" from the render outcome instead of guessing
export type IconRenderResult = "image" | "fallbackText" | "empty";

type Props = {
  // id はエラー状態を id 変化でリセットする役割も兼ねるため number/guid どちらも受け取る
  // id also resets error state on change, so it accepts both numeric ids and guids
  id: string | number;
  src: string;
  alt: string;
  className?: string;
  fallback: IconFallback;
  onRenderResult?: (result: IconRenderResult) => void;
};

type ErroredTarget = { id: string | number; src: string; connection: ConnectionStatus };

// ゲーム内アイコンの画像表示とIDフォールバックを共通化する
// Shares image rendering and the id fallback across game icons
export default function GameIcon({ id, src, alt, className, fallback, onRenderResult }: Props) {
  const { t } = useI18n();
  const connection = useConnectionStatus();
  const [erroredTarget, setErroredTarget] = useState<ErroredTarget | null>(null);

  // 配信元が変わり得る条件（id・src・接続状態）が揃う間だけラッチする
  // Latch only while every condition that could change the source (id, src, connection) still matches
  // ゲーム起動前の503や通信断は接続状態が動いた時点で解け、恒久404だけが同じ接続の間ラッチされ続ける
  // A pre-startup 503 or a dropped connection clears as soon as the connection moves on; only a permanent 404 stays latched within one connection
  const errored = erroredTarget !== null && erroredTarget.id === id && erroredTarget.src === src && erroredTarget.connection === connection;
  const renderResult: IconRenderResult = !errored ? "image" : fallback.kind === "none" ? "empty" : "fallbackText";

  // 描画結果は確定してから通知する。レンダー中に親を更新しないため副作用側から流す
  // The outcome is reported once it settles: pushing it from an effect keeps the parent out of this render
  useEffect(() => {
    onRenderResult?.(renderResult);
  }, [renderResult, onRenderResult]);

  // 文字は内側spanが受け持つ。1行省略を効かせる要素をflex中央寄せの外側と分けないと枠外へ溢れる
  // The inner span carries the text: the ellipsized line needs its own box, separate from the centering flex outside
  function fallbackTextElement(text: string) {
    return <span className={`${styles.fallback} ${className ?? ""}`}>
      <span className={styles.fallbackText}>{text}</span>
    </span>;
  }

  if (errored) {
    // 3バリアントの網羅をコンパイラに担保させる。バリアント追加時にここが必ず落ちる
    // The compiler guarantees all three variants are handled, so adding one breaks this switch first
    switch (fallback.kind) {
      case "none":
        return null;
      case "label":
        return fallbackTextElement(fallback.text);
      case "idText":
        return fallbackTextElement(t(L.ui.common.iconIdFallback, { id }));
      default: {
        const exhaustive: never = fallback;
        return exhaustive;
      }
    }
  }

  return <img src={src} alt={alt} className={className} draggable={false} onError={() => setErroredTarget({ id, src, connection })} />;
}
