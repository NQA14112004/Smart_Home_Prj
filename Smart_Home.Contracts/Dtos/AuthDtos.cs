namespace Smart_Home.Contracts;

/// <summary>Login credentials posted to POST /api/auth/login by the mobile app.</summary>
public sealed record LoginRequest(string Username, string Password);

/// <summary>JWT bundle returned on successful login. ExpiresAtUtc is the access-token expiry.</summary>
public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAtUtc,
    string FullName,
    string Role);
