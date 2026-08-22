import { useState } from "react";
import { useConnectionStatus, type ConnectionStatus } from "@/bridge";
import { L, useI18n } from "@/shared/i18n";
import styles from "./style.module.css";

// fallbackは「未指定」をundefinedセンチネルで表さず判別リテラルにする
// fallback is a discriminated literal, not an undefined sentinel for "unspecified"
export type IconFallback = { kind: "idText" } | { kind: "none" };

type Props = {
  // id はエラー状態を id 変化でリセットする役割も兼ねるため number/guid どちらも受け取る
  // id also resets error state on change, so it accepts both numeric ids and guids
  id: string | number;
  src: string;
  alt: string;
  className?: string;
  fallback: IconFallback;
};

type ErroredTarget = { id: string | number; src: string; connection: ConnectionStatus };

// ゲーム内アイコンの画像表示とIDフォールバックを共通化する
// Shares image rendering and the id fallback across game icons
export default function GameIcon({ id, src, alt, className, fallback }: Props) {
  const { t } = useI18n();
  const connection = useConnectionStatus();
  const [erroredTarget, setErroredTarget] = useState<ErroredTarget | null>(null);

  // 配信元が変わり得る条件（id・src・接続状態）が揃う間だけラッチする
  // Latch only while every condition that could change the source (id, src, connection) still matches
  // ゲーム起動前の503や通信断は接続状態が動いた時点で解け、恒久404だけが同じ接続の間ラッチされ続ける
  // A pre-startup 503 or a dropped connection clears as soon as the connection moves on; only a permanent 404 stays latched within one connection
  if (erroredTarget !== null && erroredTarget.id === id && erroredTarget.src === src && erroredTarget.connection === connection) {
    if (fallback.kind === "none") return null;
    return <span className={`${styles.fallback} ${className ?? ""}`}>
      {t(L.ui.common.iconIdFallback, { id })}
    </span>;
  }

  return <img src={src} alt={alt} className={className} draggable={false} onError={() => setErroredTarget({ id, src, connection })} />;
}
