import {
  createGuidString,
  decodeRemoteMessage,
  encodeRemoteMessage,
  RemoteMessageKind,
  RemoteProtocol,
} from "./protocol.js";

export const RemoteMethods = Object.freeze({
  PreviewCapabilitiesGet: "diagnostics.preview.capabilities.get",
  PreviewSnapshotGet: "diagnostics.preview.snapshot.get",
  TreeSnapshotGet: "diagnostics.tree.snapshot.get",
  SelectionGet: "diagnostics.selection.get",
  PropertiesSnapshotGet: "diagnostics.properties.snapshot.get",
  Elements3DSnapshotGet: "diagnostics.elements3d.snapshot.get",
  CodeDocumentsGet: "diagnostics.code.documents.get",
  CodeResolveNode: "diagnostics.code.resolve-node",
  BindingsSnapshotGet: "diagnostics.bindings.snapshot.get",
  StylesSnapshotGet: "diagnostics.styles.snapshot.get",
  ResourcesSnapshotGet: "diagnostics.resources.snapshot.get",
  AssetsSnapshotGet: "diagnostics.assets.snapshot.get",
  EventsSnapshotGet: "diagnostics.events.snapshot.get",
  BreakpointsSnapshotGet: "diagnostics.breakpoints.snapshot.get",
  LogsSnapshotGet: "diagnostics.logs.snapshot.get",
  InspectHovered: "diagnostics.inspect.hovered",
  SelectionSet: "diagnostics.selection.set",
  PreviewPausedSet: "diagnostics.preview.paused.set",
  PreviewSettingsSet: "diagnostics.preview.settings.set",
  PreviewInputInject: "diagnostics.preview.input.inject",
  Elements3DFiltersSet: "diagnostics.elements3d.filters.set",
  PropertiesSet: "diagnostics.properties.set",
  PseudoClassSet: "diagnostics.state.pseudoclass.set",
  CodeDocumentOpen: "diagnostics.code.document.open",
  BreakpointsPropertyAdd: "diagnostics.breakpoints.property.add",
  BreakpointsEventAdd: "diagnostics.breakpoints.event.add",
  BreakpointsRemove: "diagnostics.breakpoints.remove",
  BreakpointsToggle: "diagnostics.breakpoints.toggle",
  BreakpointsClear: "diagnostics.breakpoints.clear",
  BreakpointsEnabledSet: "diagnostics.breakpoints.enabled.set",
  EventsClear: "diagnostics.events.clear",
  EventsNodeEnabledSet: "diagnostics.events.node.enabled.set",
  EventsDefaultsEnable: "diagnostics.events.defaults.enable",
  EventsDisableAll: "diagnostics.events.disable-all",
  LogsClear: "diagnostics.logs.clear",
  LogsLevelsSet: "diagnostics.logs.levels.set",
  MetricsPausedSet: "diagnostics.metrics.paused.set",
  ProfilerPausedSet: "diagnostics.profiler.paused.set",
});

export const RemoteStreamTopics = Object.freeze({
  Selection: "diagnostics.stream.selection",
  Preview: "diagnostics.stream.preview",
  Metrics: "diagnostics.stream.metrics",
  Profiler: "diagnostics.stream.profiler",
  Logs: "diagnostics.stream.logs",
  Events: "diagnostics.stream.events",
});

export class RemoteAttachClient {
  constructor() {
    this.socket = null;
    this.sessionId = null;
    this.nextRequestId = 1n;
    this.pending = new Map();
    this.handlers = new Map();
    this.connectedUrl = null;
    this.transportStats = this.createTransportStats();
  }

  on(eventName, handler) {
    if (!this.handlers.has(eventName)) {
      this.handlers.set(eventName, new Set());
    }

    const bucket = this.handlers.get(eventName);
    bucket.add(handler);
    return () => bucket.delete(handler);
  }

  get isConnected() {
    return this.socket?.readyState === WebSocket.OPEN;
  }

  getTransportStats() {
    return {
      ...this.transportStats,
      timeline: [...this.transportStats.timeline],
    };
  }

  async connect(options) {
    const settings = {
      url: options?.url?.trim(),
      clientName: options?.clientName?.trim() || "prodiagnostics-web-client",
      requestedFeatures: Array.isArray(options?.requestedFeatures)
        ? options.requestedFeatures
        : [
            "read-only",
            "mutation",
            "streaming",
            "trees",
            "selection",
            "properties",
            "preview",
            "code",
            "bindings",
            "styles",
            "resources",
            "assets",
            "overlay",
            "breakpoints",
            "events",
            "logs",
            "metrics",
            "profiler",
          ],
    };

    if (!settings.url) {
      throw new Error("Connection URL is required.");
    }

    if (this.socket) {
      await this.disconnect("Reconnect");
    }

    this.transportStats = this.createTransportStats();
    this.sessionId = createGuidString();
    this.connectedUrl = settings.url;
    this.emit("status", { state: "connecting", url: settings.url });

    await new Promise((resolve, reject) => {
      const ws = new WebSocket(settings.url);
      ws.binaryType = "arraybuffer";
      this.socket = ws;

      let settled = false;
      const settle = (callback, value) => {
        if (settled) {
          return;
        }

        settled = true;
        callback(value);
      };

      const timeoutId = setTimeout(() => {
        settle(reject, new Error("Connection timeout."));
      }, 8000);

      ws.onopen = () => {
        clearTimeout(timeoutId);
        this.emit("status", { state: "online", url: settings.url });
        settle(resolve, null);
      };

      ws.onmessage = (event) => this.handleSocketMessage(event);
      ws.onerror = () => {
        if (!settled) {
          clearTimeout(timeoutId);
          settle(reject, new Error("WebSocket connection failed."));
        }
      };

      ws.onclose = (event) => this.handleSocketClosed(event);
    });

    const handshakePromise = this.waitForHelloHandshake(8000);

    await this.send({
      kind: RemoteMessageKind.Hello,
      sessionId: this.sessionId,
      processId: 0,
      processName: "browser",
      applicationName: window.location.pathname || "prodiagnostics-web-client",
      machineName: window.location.hostname || "localhost",
      runtimeVersion: navigator.userAgent,
      clientName: settings.clientName,
      requestedFeatures: settings.requestedFeatures,
    });

    try {
      await handshakePromise;
    } catch (error) {
      await this.disconnect("Handshake failed");
      throw error;
    }
  }

  async disconnect(reason = "Client disconnect") {
    const ws = this.socket;
    if (!ws) {
      this.rejectPending(new Error("Disconnected."));
      this.emit("status", { state: "offline", url: null });
      return;
    }

    if (ws.readyState === WebSocket.OPEN) {
      try {
        const message = {
          kind: RemoteMessageKind.Disconnect,
          sessionId: this.sessionId ?? createGuidString(),
          reason,
        };
        const frame = encodeRemoteMessage(message);
        ws.send(frame);
        this.trackOutgoing(message, frame.byteLength);
      } catch {
        // Best effort disconnect signal.
      }
    }

    this.socket = null;
    this.connectedUrl = null;
    this.rejectPending(new Error(`Disconnected: ${reason}`));

    try {
      ws.close(1000, reason.slice(0, 120));
    } catch {
      // no-op
    }

    this.emit("status", { state: "offline", url: null });
  }

  async send(message) {
    const ws = this.socket;
    if (!ws || ws.readyState !== WebSocket.OPEN) {
      throw new Error("Remote connection is not open.");
    }

    const frame = encodeRemoteMessage(message);
    ws.send(frame);
    this.trackOutgoing(message, frame.byteLength);
  }

  async request(method, payload, options = {}) {
    if (!method || typeof method !== "string") {
      throw new Error("Request method is required.");
    }

    const requestId = this.nextRequestId;
    this.nextRequestId += 1n;
    const key = requestId.toString();
    const timeoutMs =
      typeof options.timeoutMs === "number" && options.timeoutMs > 0
        ? options.timeoutMs
        : 60000;

    const requestMessage = {
      kind: RemoteMessageKind.Request,
      sessionId: this.sessionId ?? createGuidString(),
      requestId,
      method,
      payloadJson: payload == null ? "{}" : JSON.stringify(payload),
    };

    const responsePromise = new Promise((resolve, reject) => {
      const timeoutId = setTimeout(() => {
        this.pending.delete(key);
        reject(new Error(`Remote request timed out: ${method}`));
      }, timeoutMs);

      this.pending.set(key, {
        method,
        resolve,
        reject,
        timeoutId,
      });
    });

    try {
      await this.send(requestMessage);
    } catch (error) {
      const pending = this.pending.get(key);
      if (pending) {
        clearTimeout(pending.timeoutId);
        this.pending.delete(key);
      }

      throw error;
    }

    return responsePromise;
  }

  createTransportStats() {
    return {
      connectedAtUtc: null,
      disconnectedAtUtc: null,
      sentMessages: 0,
      receivedMessages: 0,
      sentBytes: 0,
      receivedBytes: 0,
      keepAliveCount: 0,
      lastSendUtc: null,
      lastReceiveUtc: null,
      timeline: [],
    };
  }

  handleSocketMessage(event) {
    try {
      const bytes = getEventMessageSize(event.data);
      const message = decodeRemoteMessage(event.data);
      this.trackIncoming(message, bytes);
      this.emit("message", message);
      this.pushTimeline("in", message.kindName, bytes, buildTimelineSummary(message));

      switch (message.kind) {
        case RemoteMessageKind.HelloAck:
          this.emit("helloAck", message);
          return;
        case RemoteMessageKind.HelloReject:
          this.emit("helloReject", message);
          this.rejectPending(
            new Error(`Remote attach rejected: ${message.reason} ${message.details}`.trim()),
          );
          return;
        case RemoteMessageKind.KeepAlive:
          this.transportStats.keepAliveCount += 1;
          this.emit("keepAlive", message);
          this.emit("transport", this.getTransportStats());
          return;
        case RemoteMessageKind.Response:
          this.handleResponseMessage(message);
          return;
        case RemoteMessageKind.Stream:
          this.handleStreamMessage(message);
          return;
        case RemoteMessageKind.Error:
          this.emit("remoteError", message);
          return;
        case RemoteMessageKind.Disconnect:
          this.emit("remoteDisconnect", message);
          this.disconnect(message.reason || "Remote disconnected");
          return;
        default:
          return;
      }
    } catch (error) {
      const normalized = normalizeError(error);
      this.rejectPending(new Error(`Remote protocol decode failed: ${normalized}`));
      this.emit("clientError", normalized);
    }
  }

  handleSocketClosed(event) {
    this.transportStats.disconnectedAtUtc = new Date().toISOString();
    const reason = event?.reason?.trim() || "Socket closed";
    this.rejectPending(new Error(reason));
    this.emit("status", { state: "offline", url: this.connectedUrl, reason });
  }

  handleResponseMessage(message) {
    const key = normalizeRequestId(message.requestId);
    const pending = this.pending.get(key);
    if (!pending) {
      this.emit("unmatchedResponse", message);
      return;
    }

    clearTimeout(pending.timeoutId);
    this.pending.delete(key);

    if (!message.isSuccess) {
      const errorMessage = [message.errorCode, message.errorMessage]
        .filter(Boolean)
        .join(": ");
      pending.reject(new Error(errorMessage || "Remote request failed."));
      return;
    }

    let payload = {};
    if (message.payloadJson && message.payloadJson.trim().length > 0) {
      try {
        payload = JSON.parse(message.payloadJson);
      } catch (error) {
        pending.reject(
          new Error(`Remote payload parse failed for ${pending.method}: ${normalizeError(error)}`),
        );
        return;
      }
    }

    pending.resolve({
      requestId: message.requestId,
      method: pending.method,
      payload,
      rawMessage: message,
    });
  }

  handleStreamMessage(message) {
    let payload = null;
    try {
      payload = message.payloadJson ? JSON.parse(message.payloadJson) : null;
    } catch (error) {
      this.emit(
        "clientError",
        `Stream payload parse failed for topic ${message.topic}: ${normalizeError(error)}`,
      );
    }

    const packet = {
      topic: message.topic,
      sequence: message.sequence,
      droppedMessages: message.droppedMessages ?? 0,
      payload,
      rawMessage: message,
    };

    this.emit("stream", packet);
    this.emit(`stream:${message.topic}`, packet);
  }

  trackOutgoing(message, byteLength) {
    const now = new Date().toISOString();
    this.transportStats.sentMessages += 1;
    this.transportStats.sentBytes += byteLength;
    this.transportStats.lastSendUtc = now;
    if (!this.transportStats.connectedAtUtc) {
      this.transportStats.connectedAtUtc = now;
    }

    this.pushTimeline("out", message.kindName ?? message.kind, byteLength, buildTimelineSummary(message));
    this.emit("transport", this.getTransportStats());
  }

  trackIncoming(message, byteLength) {
    const now = new Date().toISOString();
    this.transportStats.receivedMessages += 1;
    this.transportStats.receivedBytes += byteLength;
    this.transportStats.lastReceiveUtc = now;
    this.emit("transport", this.getTransportStats());
  }

  pushTimeline(direction, kindName, bytes, summary) {
    this.transportStats.timeline.push({
      timestampUtc: new Date().toISOString(),
      direction,
      kindName,
      bytes,
      summary,
    });

    if (this.transportStats.timeline.length > 600) {
      this.transportStats.timeline.splice(0, this.transportStats.timeline.length - 600);
    }
  }

  rejectPending(error) {
    for (const [key, pending] of this.pending.entries()) {
      clearTimeout(pending.timeoutId);
      pending.reject(error);
      this.pending.delete(key);
    }
  }

  emit(eventName, payload) {
    const bucket = this.handlers.get(eventName);
    if (!bucket || bucket.size === 0) {
      return;
    }

    for (const handler of bucket) {
      try {
        handler(payload);
      } catch {
        // Event listener failures must not break transport loop.
      }
    }
  }

  waitForHelloHandshake(timeoutMs) {
    return new Promise((resolve, reject) => {
      let settled = false;
      const timeoutId = setTimeout(() => {
        if (settled) {
          return;
        }

        settled = true;
        cleanup();
        reject(new Error("Timed out waiting for HelloAck."));
      }, Math.max(1000, Number(timeoutMs) || 8000));

      const cleanup = () => {
        offAck();
        offReject();
      };

      const offAck = this.on("helloAck", (message) => {
        if (settled) {
          return;
        }

        settled = true;
        clearTimeout(timeoutId);
        cleanup();
        resolve(message);
      });

      const offReject = this.on("helloReject", (message) => {
        if (settled) {
          return;
        }

        settled = true;
        clearTimeout(timeoutId);
        cleanup();
        const details = [message?.reason, message?.details].filter(Boolean).join(": ");
        reject(new Error(details || "Remote attach rejected."));
      });
    });
  }
}

function normalizeRequestId(requestId) {
  if (typeof requestId === "bigint") {
    return requestId.toString();
  }

  if (typeof requestId === "number") {
    return String(Math.trunc(requestId));
  }

  return String(requestId);
}

function buildTimelineSummary(message) {
  switch (message.kind) {
    case RemoteMessageKind.Request:
      return `${message.method} #${message.requestId}`;
    case RemoteMessageKind.Response:
      return `#${message.requestId} ${message.isSuccess ? "ok" : "error"}`;
    case RemoteMessageKind.Stream:
      return `${message.topic} seq=${message.sequence}`;
    case RemoteMessageKind.KeepAlive:
      return `seq=${message.sequence}`;
    case RemoteMessageKind.Error:
      return `${message.errorCode}`;
    case RemoteMessageKind.Hello:
      return `${message.clientName}`;
    case RemoteMessageKind.HelloAck:
      return `v${message.negotiatedProtocolVersion}`;
    case RemoteMessageKind.HelloReject:
      return `${message.reason}`;
    case RemoteMessageKind.Disconnect:
      return message.reason;
    default:
      return "";
  }
}

function getEventMessageSize(data) {
  if (data instanceof ArrayBuffer) {
    return data.byteLength;
  }

  if (ArrayBuffer.isView(data)) {
    return data.byteLength;
  }

  if (typeof data === "string") {
    return data.length;
  }

  return 0;
}

function normalizeError(error) {
  if (!error) {
    return "Unknown error";
  }

  if (error instanceof Error) {
    return error.message || error.name;
  }

  return String(error);
}
