const textEncoder = new TextEncoder();
const textDecoder = new TextDecoder("utf-8", { fatal: true });

export const RemoteMessageKind = Object.freeze({
  Hello: 1,
  HelloAck: 2,
  HelloReject: 3,
  KeepAlive: 4,
  Disconnect: 5,
  Request: 6,
  Response: 7,
  Stream: 8,
  Error: 9,
});

const remoteMessageKindNames = new Map(
  Object.entries(RemoteMessageKind).map(([name, value]) => [value, name]),
);

export const RemoteProtocol = Object.freeze({
  Version: 1,
  HeaderSizeBytes: 6,
  MaxFramePayloadBytes: 16 * 1024 * 1024,
  MaxListEntries: 1024,
});

export function getMessageKindName(kind) {
  return remoteMessageKindNames.get(kind) ?? `Unknown(${kind})`;
}

export function createGuidString() {
  const bytes = crypto.getRandomValues(new Uint8Array(16));
  bytes[7] = (bytes[7] & 0x0f) | 0x40;
  bytes[8] = (bytes[8] & 0x3f) | 0x80;
  return dotNetGuidBytesToString(bytes);
}

export function encodeRemoteMessage(message) {
  const kind = message?.kind;
  if (!remoteMessageKindNames.has(kind)) {
    throw new Error(`Unsupported message kind: ${kind}`);
  }

  const writer = new PayloadWriter();
  writePayload(writer, message);
  const payload = writer.toUint8Array();
  if (payload.length > RemoteProtocol.MaxFramePayloadBytes) {
    throw new Error(
      `Payload exceeds max frame size (${RemoteProtocol.MaxFramePayloadBytes} bytes).`,
    );
  }

  const frame = new Uint8Array(RemoteProtocol.HeaderSizeBytes + payload.length);
  const view = new DataView(frame.buffer);
  frame[0] = RemoteProtocol.Version;
  frame[1] = kind;
  view.setInt32(2, payload.length, true);
  frame.set(payload, RemoteProtocol.HeaderSizeBytes);
  return frame.buffer;
}

export function decodeRemoteMessage(frameLike) {
  const frame = toUint8Array(frameLike);
  if (frame.length < RemoteProtocol.HeaderSizeBytes) {
    throw new Error("Frame is shorter than protocol header.");
  }

  const view = new DataView(frame.buffer, frame.byteOffset, frame.byteLength);
  const version = frame[0];
  if (version !== RemoteProtocol.Version) {
    throw new Error(`Unsupported protocol version ${version}.`);
  }

  const kind = frame[1];
  if (!remoteMessageKindNames.has(kind)) {
    throw new Error(`Unknown message kind ${kind}.`);
  }

  const payloadLength = view.getInt32(2, true);
  if (
    payloadLength < 0 ||
    payloadLength > RemoteProtocol.MaxFramePayloadBytes ||
    payloadLength !== frame.length - RemoteProtocol.HeaderSizeBytes
  ) {
    throw new Error("Frame payload length is invalid.");
  }

  const payload = frame.subarray(
    RemoteProtocol.HeaderSizeBytes,
    RemoteProtocol.HeaderSizeBytes + payloadLength,
  );
  const reader = new PayloadReader(payload);
  const message = readPayload(kind, reader);

  if (reader.remaining !== 0) {
    throw new Error("Payload has trailing unread bytes.");
  }

  return {
    ...message,
    kind,
    kindName: getMessageKindName(kind),
  };
}

function writePayload(writer, message) {
  switch (message.kind) {
    case RemoteMessageKind.Hello:
      writer.writeGuid(message.sessionId);
      writer.writeInt32(message.processId ?? 0);
      writer.writeString(message.processName ?? "");
      writer.writeString(message.applicationName ?? "");
      writer.writeString(message.machineName ?? "");
      writer.writeString(message.runtimeVersion ?? "");
      writer.writeString(message.clientName ?? "");
      writer.writeStringList(message.requestedFeatures ?? []);
      return;
    case RemoteMessageKind.HelloAck:
      writer.writeGuid(message.sessionId);
      writer.writeUint8(message.negotiatedProtocolVersion ?? RemoteProtocol.Version);
      writer.writeStringList(message.enabledFeatures ?? []);
      return;
    case RemoteMessageKind.HelloReject:
      writer.writeGuid(message.sessionId);
      writer.writeString(message.reason ?? "");
      writer.writeString(message.details ?? "");
      return;
    case RemoteMessageKind.KeepAlive:
      writer.writeGuid(message.sessionId);
      writer.writeInt64(message.sequence ?? 0);
      writer.writeInt64(normalizeTimestampMs(message.timestampUtc));
      return;
    case RemoteMessageKind.Disconnect:
      writer.writeGuid(message.sessionId);
      writer.writeString(message.reason ?? "");
      return;
    case RemoteMessageKind.Request:
      writer.writeGuid(message.sessionId);
      writer.writeInt64(message.requestId ?? 0);
      writer.writeString(message.method ?? "");
      writer.writeString(message.payloadJson ?? "");
      return;
    case RemoteMessageKind.Response:
      writer.writeGuid(message.sessionId);
      writer.writeInt64(message.requestId ?? 0);
      writer.writeBoolean(Boolean(message.isSuccess));
      writer.writeString(message.payloadJson ?? "");
      writer.writeString(message.errorCode ?? "");
      writer.writeString(message.errorMessage ?? "");
      return;
    case RemoteMessageKind.Stream:
      writer.writeGuid(message.sessionId);
      writer.writeString(message.topic ?? "");
      writer.writeInt64(message.sequence ?? 0);
      writer.writeInt32(message.droppedMessages ?? 0);
      writer.writeString(message.payloadJson ?? "");
      return;
    case RemoteMessageKind.Error:
      writer.writeGuid(message.sessionId);
      writer.writeString(message.errorCode ?? "");
      writer.writeString(message.errorMessage ?? "");
      writer.writeInt64(message.relatedRequestId ?? 0);
      return;
    default:
      throw new Error(`Unsupported message kind ${message.kind}.`);
  }
}

function readPayload(kind, reader) {
  switch (kind) {
    case RemoteMessageKind.Hello:
      return {
        sessionId: reader.readGuid(),
        processId: reader.readInt32(),
        processName: reader.readString(),
        applicationName: reader.readString(),
        machineName: reader.readString(),
        runtimeVersion: reader.readString(),
        clientName: reader.readString(),
        requestedFeatures: reader.readStringList(),
      };
    case RemoteMessageKind.HelloAck:
      return {
        sessionId: reader.readGuid(),
        negotiatedProtocolVersion: reader.readUint8(),
        enabledFeatures: reader.readStringList(),
      };
    case RemoteMessageKind.HelloReject:
      return {
        sessionId: reader.readGuid(),
        reason: reader.readString(),
        details: reader.readString(),
      };
    case RemoteMessageKind.KeepAlive: {
      const sessionId = reader.readGuid();
      const sequence = reader.readInt64AsSafeValue();
      const timestampMs = reader.readInt64AsNumber();
      return {
        sessionId,
        sequence,
        timestampUtc: new Date(timestampMs).toISOString(),
        timestampMs,
      };
    }
    case RemoteMessageKind.Disconnect:
      return {
        sessionId: reader.readGuid(),
        reason: reader.readString(),
      };
    case RemoteMessageKind.Request:
      return {
        sessionId: reader.readGuid(),
        requestId: reader.readInt64AsSafeValue(),
        method: reader.readString(),
        payloadJson: reader.readString(),
      };
    case RemoteMessageKind.Response:
      return {
        sessionId: reader.readGuid(),
        requestId: reader.readInt64AsSafeValue(),
        isSuccess: reader.readBoolean(),
        payloadJson: reader.readString(),
        errorCode: reader.readString(),
        errorMessage: reader.readString(),
      };
    case RemoteMessageKind.Stream:
      return {
        sessionId: reader.readGuid(),
        topic: reader.readString(),
        sequence: reader.readInt64AsSafeValue(),
        droppedMessages: reader.readInt32(),
        payloadJson: reader.readString(),
      };
    case RemoteMessageKind.Error:
      return {
        sessionId: reader.readGuid(),
        errorCode: reader.readString(),
        errorMessage: reader.readString(),
        relatedRequestId: reader.readInt64AsSafeValue(),
      };
    default:
      throw new Error(`Unsupported message kind ${kind}.`);
  }
}

function normalizeTimestampMs(value) {
  if (value instanceof Date) {
    return BigInt(value.getTime());
  }

  if (typeof value === "string") {
    const parsed = Date.parse(value);
    return BigInt(Number.isFinite(parsed) ? parsed : Date.now());
  }

  if (typeof value === "number") {
    return BigInt(Math.trunc(value));
  }

  if (typeof value === "bigint") {
    return value;
  }

  return BigInt(Date.now());
}

function toUint8Array(value) {
  if (value instanceof Uint8Array) {
    return value;
  }

  if (value instanceof ArrayBuffer) {
    return new Uint8Array(value);
  }

  if (ArrayBuffer.isView(value)) {
    return new Uint8Array(value.buffer, value.byteOffset, value.byteLength);
  }

  throw new Error("Expected binary frame payload.");
}

class PayloadWriter {
  constructor(initialCapacity = 512) {
    this.buffer = new Uint8Array(initialCapacity);
    this.offset = 0;
  }

  toUint8Array() {
    return this.buffer.slice(0, this.offset);
  }

  ensure(sizeHint) {
    const required = this.offset + sizeHint;
    if (required <= this.buffer.length) {
      return;
    }

    let next = this.buffer.length;
    while (next < required) {
      next *= 2;
    }

    const expanded = new Uint8Array(next);
    expanded.set(this.buffer, 0);
    this.buffer = expanded;
  }

  writeUint8(value) {
    this.ensure(1);
    this.buffer[this.offset] = value & 0xff;
    this.offset += 1;
  }

  writeBoolean(value) {
    this.writeUint8(value ? 1 : 0);
  }

  writeInt32(value) {
    this.ensure(4);
    const view = new DataView(this.buffer.buffer);
    view.setInt32(this.offset, value | 0, true);
    this.offset += 4;
  }

  writeInt64(value) {
    this.ensure(8);
    const view = new DataView(this.buffer.buffer);
    view.setBigInt64(this.offset, normalizeInt64(value), true);
    this.offset += 8;
  }

  writeGuid(guid) {
    this.writeBytes(guidStringToDotNetBytes(guid));
  }

  writeString(value) {
    const safe = value == null ? "" : String(value);
    const bytes = textEncoder.encode(safe);
    this.writeInt32(bytes.length);
    this.writeBytes(bytes);
  }

  writeStringList(values) {
    const list = Array.isArray(values) ? values : [];
    if (list.length > RemoteProtocol.MaxListEntries) {
      throw new Error(
        `String list exceeds max entry count ${RemoteProtocol.MaxListEntries}.`,
      );
    }

    this.writeInt32(list.length);
    for (const value of list) {
      this.writeString(value ?? "");
    }
  }

  writeBytes(bytes) {
    this.ensure(bytes.length);
    this.buffer.set(bytes, this.offset);
    this.offset += bytes.length;
  }
}

class PayloadReader {
  constructor(bytes) {
    this.bytes = bytes;
    this.view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
    this.offset = 0;
  }

  get remaining() {
    return this.bytes.length - this.offset;
  }

  ensure(length) {
    if (this.offset + length > this.bytes.length) {
      throw new Error("Unexpected end of payload.");
    }
  }

  readUint8() {
    this.ensure(1);
    const value = this.view.getUint8(this.offset);
    this.offset += 1;
    return value;
  }

  readBoolean() {
    const value = this.readUint8();
    if (value !== 0 && value !== 1) {
      throw new Error("Invalid boolean payload value.");
    }

    return value === 1;
  }

  readInt32() {
    this.ensure(4);
    const value = this.view.getInt32(this.offset, true);
    this.offset += 4;
    return value;
  }

  readInt64() {
    this.ensure(8);
    const value = this.view.getBigInt64(this.offset, true);
    this.offset += 8;
    return value;
  }

  readInt64AsNumber() {
    const value = this.readInt64();
    const asNumber = Number(value);
    if (!Number.isFinite(asNumber)) {
      throw new Error("64-bit integer value is not representable as number.");
    }

    return asNumber;
  }

  readInt64AsSafeValue() {
    const value = this.readInt64();
    if (
      value >= BigInt(Number.MIN_SAFE_INTEGER) &&
      value <= BigInt(Number.MAX_SAFE_INTEGER)
    ) {
      return Number(value);
    }

    return value;
  }

  readGuid() {
    this.ensure(16);
    const guidBytes = this.bytes.subarray(this.offset, this.offset + 16);
    this.offset += 16;
    return dotNetGuidBytesToString(guidBytes);
  }

  readString() {
    const byteCount = this.readInt32();
    if (byteCount < 0) {
      throw new Error("Negative string length in payload.");
    }

    if (byteCount === 0) {
      return "";
    }

    this.ensure(byteCount);
    const value = textDecoder.decode(
      this.bytes.subarray(this.offset, this.offset + byteCount),
    );
    this.offset += byteCount;
    return value;
  }

  readStringList() {
    const count = this.readInt32();
    if (count < 0 || count > RemoteProtocol.MaxListEntries) {
      throw new Error(`Invalid string list count ${count}.`);
    }

    const values = [];
    for (let i = 0; i < count; i += 1) {
      values.push(this.readString());
    }

    return values;
  }
}

function normalizeInt64(value) {
  if (typeof value === "bigint") {
    return value;
  }

  if (typeof value === "number") {
    if (!Number.isFinite(value)) {
      return 0n;
    }

    return BigInt(Math.trunc(value));
  }

  if (typeof value === "string" && value.length > 0) {
    try {
      return BigInt(value);
    } catch {
      return 0n;
    }
  }

  return 0n;
}

function guidStringToDotNetBytes(value) {
  const text = String(value ?? "").trim().toLowerCase();
  const match =
    /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/.exec(
      text,
    );
  if (!match) {
    throw new Error(`Invalid GUID string "${value}".`);
  }

  const [seg1, seg2, seg3, seg4, seg5] = text.split("-");
  const bytes = new Uint8Array(16);
  const d1 = parseInt(seg1, 16);
  const d2 = parseInt(seg2, 16);
  const d3 = parseInt(seg3, 16);

  bytes[0] = d1 & 0xff;
  bytes[1] = (d1 >> 8) & 0xff;
  bytes[2] = (d1 >> 16) & 0xff;
  bytes[3] = (d1 >> 24) & 0xff;
  bytes[4] = d2 & 0xff;
  bytes[5] = (d2 >> 8) & 0xff;
  bytes[6] = d3 & 0xff;
  bytes[7] = (d3 >> 8) & 0xff;

  const tailHex = seg4 + seg5;
  for (let i = 0; i < 8; i += 1) {
    const start = i * 2;
    bytes[8 + i] = parseInt(tailHex.slice(start, start + 2), 16);
  }

  return bytes;
}

function dotNetGuidBytesToString(bytesLike) {
  const bytes = toUint8Array(bytesLike);
  if (bytes.length < 16) {
    throw new Error("GUID byte payload requires 16 bytes.");
  }

  const d1 =
    bytes[0] |
    (bytes[1] << 8) |
    (bytes[2] << 16) |
    (bytes[3] << 24);
  const d2 = bytes[4] | (bytes[5] << 8);
  const d3 = bytes[6] | (bytes[7] << 8);

  const seg1 = toHexUnsigned(d1, 8);
  const seg2 = toHexUnsigned(d2, 4);
  const seg3 = toHexUnsigned(d3, 4);
  const seg4 = `${toHexUnsigned(bytes[8], 2)}${toHexUnsigned(bytes[9], 2)}`;
  const seg5 =
    `${toHexUnsigned(bytes[10], 2)}` +
    `${toHexUnsigned(bytes[11], 2)}` +
    `${toHexUnsigned(bytes[12], 2)}` +
    `${toHexUnsigned(bytes[13], 2)}` +
    `${toHexUnsigned(bytes[14], 2)}` +
    `${toHexUnsigned(bytes[15], 2)}`;

  return `${seg1}-${seg2}-${seg3}-${seg4}-${seg5}`;
}

function toHexUnsigned(value, length) {
  const hex = (value >>> 0).toString(16);
  return hex.padStart(length, "0").slice(-length);
}
