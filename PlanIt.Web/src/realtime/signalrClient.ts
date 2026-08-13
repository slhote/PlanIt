import * as signalR from "@microsoft/signalr";
import { getAccessToken } from "../auth/authStore";
import { setConnectionId } from "./connectionId";

const BASE_URL = import.meta.env.VITE_API_BASE_URL as string;

let connection: signalR.HubConnection | null = null;
let startPromise: Promise<void> | null = null;
// The project group most recently joined — re-joined automatically on reconnect, since
// ConnectionId (and therefore group membership) resets when the connection drops and comes back
// (planit-api-contracts-backend.md §5: "re-verify/re-join on every connect").
let activeProjectId: string | null = null;

function createConnection(): signalR.HubConnection {
  const conn = new signalR.HubConnectionBuilder()
    .withUrl(`${BASE_URL}/hub`, { accessTokenFactory: () => getAccessToken() ?? "" })
    .withAutomaticReconnect()
    .build();

  conn.onreconnected(async () => {
    setConnectionId(conn.connectionId);
    if (activeProjectId) {
      await conn.invoke("JoinProject", activeProjectId).catch(() => {});
    }
  });
  conn.onclose(() => {
    setConnectionId(null);
  });

  return conn;
}

export async function ensureConnected(): Promise<signalR.HubConnection> {
  if (!connection) {
    connection = createConnection();
  }
  if (connection.state === signalR.HubConnectionState.Disconnected) {
    startPromise = connection.start().then(() => setConnectionId(connection!.connectionId));
  }
  if (startPromise) {
    await startPromise;
  }
  return connection;
}

export async function joinProject(projectId: string): Promise<void> {
  activeProjectId = projectId;
  const conn = await ensureConnected();
  await conn.invoke("JoinProject", projectId);
}
