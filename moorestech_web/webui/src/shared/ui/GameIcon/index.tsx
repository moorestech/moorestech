import { useState } from "react";
import { useI18n } from "@/shared/i18n";
import styles from "./style.module.css";

type Props = {
  id: number;
  src: string;
  alt: string;
  className?: string;
};

// ゲーム内アイコンの画像表示とIDフォールバックを共通化する
// Shares image rendering and the id fallback across game icons
export default function GameIcon({ id, src, alt, className }: Props) {
  const { t } = useI18n();
  const [erroredId, setErroredId] = useState<number | null>(null);

  if (erroredId === id) {
    return <span className={`${styles.fallback} ${className ?? ""}`}>{t("#{id}", { id })}</span>;
  }

  return <img src={src} alt={alt} className={className} draggable={false} onError={() => setErroredId(id)} />;
}
