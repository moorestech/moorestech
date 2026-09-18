import contract from "../contract.json";

// C#クライアントと共有する契約値の正本はcontract.json。Worker側はここ経由でだけ参照する
// contract.json is the single source for values shared with the C# client; the Worker reads them only through here
export const STEAM_IDENTITY: string = contract.steamIdentity;
export const MAX_FILE_BYTES: number = contract.maxFileBytes;
export const MAX_BUNDLE_FILES: number = contract.maxBundleFiles;
export const MAX_BUNDLE_BYTES: number = contract.maxBundleBytes;
export const UPLOAD_URL_TTL_SECONDS: number = contract.uploadUrlTtlSeconds;
export const UPLOAD_IDLE_TIMEOUT_SECONDS: number = contract.uploadIdleTimeoutSeconds;
export const RESERVED_UPLOAD_SEGMENTS: ReadonlySet<string> = new Set(contract.reservedUploadSegments);
export const CONTRACT_KINDS: readonly string[] = contract.kinds;
export const TOKEN_TTL_SECONDS: number = contract.tokenTtlSeconds;
