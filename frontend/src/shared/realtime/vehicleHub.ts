import { HubConnectionBuilder, HubConnectionState } from "@microsoft/signalr";
import { apiBaseUrl } from "@/shared/config";
import {
  liveSnapshotMessageSchema, liveUpdateMessageSchema,
  type LiveSnapshotMessage, type LiveUpdateMessage,
} from "@/shared/contracts/live";

export type ConnectionStatus = "connecting" | "connected" | "reconnecting" | "disconnected";
type Events = {
  snapshot: LiveSnapshotMessage;
  update: LiveUpdateMessage;
  status: ConnectionStatus;
};

const RETRY_INTERVAL_MS = 5_000;

export function createVehicleHub() {
  const connection = new HubConnectionBuilder()
    .withUrl(`${apiBaseUrl}/hubs/vehicles`)
    .withAutomaticReconnect()
    .build();
  const listeners: { [K in keyof Events]: Set<(value: Events[K]) => void> } = {
    snapshot: new Set(), update: new Set(), status: new Set(),
  };
  let stopped = false;
  let starting = false;
  let hasSnapshot = false;
  let retryTimer: ReturnType<typeof setTimeout> | undefined;

  function emit<K extends keyof Events>(event: K, value: Events[K]) {
    if (!stopped) listeners[event].forEach((listener) => listener(value));
  }

  function clearRetry() {
    clearTimeout(retryTimer);
    retryTimer = undefined;
  }

  function scheduleRetry() {
    clearRetry();
    if (!stopped) retryTimer = setTimeout(() => void start({ background: true }), RETRY_INTERVAL_MS);
  }

  // background: koptuktan sonraki otomatik denemeler. Bunlar durumu "bağlanıyor"a çekmez; yoksa ekran her 5 sn'de
  // "koptu" ile "bağlanıyor" arasında gidip gelir. Kullanıcının "yeniden dene"si ise "bağlanıyor" gösterir.
  async function start({ background = false }: { background?: boolean } = {}) {
    if (stopped || starting || connection.state !== HubConnectionState.Disconnected) return;
    clearRetry();
    starting = true;
    hasSnapshot = false;
    if (!background) emit("status", "connecting");
    try {
      await connection.start();
      emit("status", "connected");
    } catch (error) {
      if (!stopped) {
        console.warn("Araç bağlantısı başlatılamadı.", error);
        emit("status", "disconnected");
        scheduleRetry();
      }
    } finally {
      starting = false;
    }
  }

  connection.on("snapshot", (value: unknown) => {
    const result = liveSnapshotMessageSchema.safeParse(value);
    if (!result.success) {
      console.warn("Geçersiz araç snapshot mesajı yok sayıldı.", result.error);
      return;
    }
    hasSnapshot = true;
    emit("snapshot", result.data);
  });
  connection.on("update", (value: unknown) => {
    const result = liveUpdateMessageSchema.safeParse(value);
    if (!result.success) {
      console.warn("Geçersiz araç update mesajı yok sayıldı.", result.error);
      return;
    }
    // Yeni bağlantının tam görüntüsü olmadan deltalar eski oturuma uygulanamaz.
    if (hasSnapshot) emit("update", result.data);
  });
  connection.onreconnecting(() => {
    hasSnapshot = false;
    emit("status", "reconnecting");
  });
  connection.onreconnected(() => emit("status", "connected"));
  connection.onclose(() => {
    hasSnapshot = false;
    emit("status", "disconnected");
    scheduleRetry();
  });

  return {
    start,
    async stop() {
      stopped = true;
      clearRetry();
      Object.values(listeners).forEach((set) => set.clear());
      await connection.stop();
    },
    on<K extends keyof Events>(event: K, listener: (value: Events[K]) => void) {
      listeners[event].add(listener);
      return () => { listeners[event].delete(listener); };
    },
  };
}
