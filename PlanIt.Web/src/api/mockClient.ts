// Simulates the latency/failure characteristics of a real network client so the
// UI has to handle loading and error states honestly, even with no backend yet.
// A real api/ module would swap this file's guts for fetch() and keep the same
// exported function signatures used by hooks/.

const LATENCY_MS = 350;

let chaosMode = false;

/** Dev-only toggle (exposed via a checkbox on the board) to force mutations to fail, so optimistic-update revert is demonstrable on demand instead of at random. */
export function setChaosMode(enabled: boolean) {
  chaosMode = enabled;
}

export function isChaosMode() {
  return chaosMode;
}

export class MockApiError extends Error {
  status: number;
  constructor(message: string, status = 400) {
    super(message);
    this.status = status;
  }
}

export async function delay<T>(value: T, ms = LATENCY_MS): Promise<T> {
  await new Promise((resolve) => setTimeout(resolve, ms));
  return value;
}

export async function mutate<T>(fn: () => T, ms = LATENCY_MS): Promise<T> {
  await new Promise((resolve) => setTimeout(resolve, ms));
  if (chaosMode) {
    throw new MockApiError("Simulated network failure (chaos mode is on).", 503);
  }
  return fn();
}

let idCounter = 1000;
export function nextId(prefix: string): string {
  idCounter += 1;
  return `${prefix}${idCounter}`;
}
