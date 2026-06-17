namespace Smart_Home.Service
{
    /// <summary>
    /// Lightweight outcome type so services report validation/uniqueness/not-found/DB failures
    /// without throwing. ViewModels map this to MessageBox; tests assert on it.
    /// </summary>
    public class OperationResult
    {
        public bool Success { get; init; }
        public string? ErrorMessage { get; init; }

        public static OperationResult Ok() => new() { Success = true };
        public static OperationResult Fail(string message) => new() { Success = false, ErrorMessage = message };
    }
}
