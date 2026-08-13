// Plain module-level singleton (matches authStore.ts's pattern) — the current SignalR
// ConnectionId, set once the hub connection is established. Read by httpClient.ts to send
// X-SignalR-Connection-Id on mutating calls, so the backend's broadcast can exclude the
// originating client (planit-api-contracts-backend.md §5). A separate module (not part of
// signalrClient.ts itself) avoids a circular import between the SignalR client and the HTTP
// client, since each needs to reference "the current connection id" independently.

let connectionId: string | null = null;

export function setConnectionId(id: string | null) {
  connectionId = id;
}

export function getConnectionId(): string | null {
  return connectionId;
}
