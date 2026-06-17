namespace Smart_Home.Contracts;

/// <summary>One entry/exit event for the mobile "xem log ra/vào" screen (projection of access_logs).</summary>
public sealed record AccessLogDto(
    long Id,
    DateTime CreatedAtUtc,
    string Method,            // RFID | PIN | FACE_RECOGNITION | REMOTE_UNLOCK | ...
    string Result,            // success | denied | ...
    string? DoorStatus,
    string? LockStatus,
    string? UserFullName,
    string? CardUid,
    string? Message);

/// <summary>Generic paged envelope for list endpoints.</summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    long Total);
