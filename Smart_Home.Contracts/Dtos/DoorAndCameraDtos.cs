namespace Smart_Home.Contracts;

/// <summary>Remote-unlock command body for POST /api/door/unlock.</summary>
public sealed record UnlockRequest(int? DurationSeconds);

/// <summary>Result of a remote-unlock request (command is published to MQTT, executed asynchronously).</summary>
public sealed record UnlockResponse(bool Accepted, string? Message);

/// <summary>
/// A "someone at the door" / security event delivered as a push notification and listed in-app.
/// Sourced from MQTT door topics (face/result, door/rfid, door/keypad, door/breach).
/// </summary>
public sealed record DoorEventDto(
    string Type,              // FACE | RFID | PIN | FORCED_ENTRY | ...
    string Title,
    string Message,
    string Level,             // info | warning | critical
    string? UserFullName,
    DateTime OccurredAtUtc);

/// <summary>Device push-token registration for POST /api/devices/push-token.</summary>
public sealed record PushTokenRequest(string Token, string Platform); // Platform: "android" | "ios"

/// <summary>
/// Tells the mobile app where to fetch the door camera feed. Paths are relative to the API base and
/// require the JWT. Snapshot = single JPEG (battery-friendly polling); Stream = MJPEG live view.
/// </summary>
public sealed record CameraInfoDto(
    bool Available,
    string SnapshotPath,      // e.g. "/api/camera/snapshot"
    string StreamPath);       // e.g. "/api/camera/stream"
