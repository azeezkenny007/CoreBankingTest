namespace CoreBankingTest.API.Models
{
   
 public record ApiResponse
    {
        public bool Success { get; init; }
        public string Message { get; init; } = string.Empty;
        public string[] Errors { get; init; } = Array.Empty<string>();
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

        public static ApiResponse CreateSuccess(string message = "Operation completed successfully")
            => new() { Success = true, Message = message };

        public static ApiResponse CreateFailure(params string[] errors)
            => new() { Success = false, Errors = errors };
    }

    public record ApiResponse<T> : ApiResponse
    {
        public T? Data { get; init; }

        public static ApiResponse<T> CreateSuccess(T data, string message = "Operation completed successfully")
            => new() { Success = true, Message = message, Data = data };

        public static new ApiResponse<T> CreateFailure(params string[] errors)
            => new() { Success = false, Errors = errors };
    }
}
