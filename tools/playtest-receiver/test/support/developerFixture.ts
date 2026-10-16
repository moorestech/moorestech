import { handle, noNetwork, workerEnv, ID } from "./uploadsFixture";

export function admin(method: string, suffix: string, body: string, key: string): Promise<Response> {
  return handle(new Request(`https://x/v1/admin/dev-tokens${suffix}`, {
    method, headers: { "x-admin-key": key }, ...(method === "POST" ? { body } : {}),
  }), workerEnv, noNetwork);
}

export async function issue(machine: string): Promise<string> {
  const response = await admin("POST", "", JSON.stringify({ machine }), workerEnv.ADMIN_KEY);
  if (response.status !== 201) throw new Error(`issue failed: ${response.status}`);
  return ((await response.json()) as { token: string }).token;
}

export function exchange(body: string): Promise<Response> {
  return handle(new Request("https://x/v1/session/developer", { method: "POST", body }), workerEnv, noNetwork);
}

export async function developerSession(): Promise<string> {
  const response = await exchange(JSON.stringify({ token: await issue("macbook") }));
  return ((await response.json()) as { token: string }).token;
}

export function upload(action: string, kind: string, token: string, body: string): Promise<Response> {
  return handle(new Request(`https://x/v1/uploads/${kind}/${ID}/${action}`, {
    method: "POST", headers: { authorization: `Bearer ${token}` }, body,
  }), workerEnv, noNetwork);
}
