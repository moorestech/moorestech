import type { z } from "zod";
import type { BlockInventoryDataSchema } from "../../../src/bridge/contract/schemas";

// モック送信専用のパース前契約を導出する
// Derive the pre-parse contract exclusively for mock payloads
export type BlockInventoryWireData = z.input<typeof BlockInventoryDataSchema>;
