namespace SecurityRecap.Api.DTOs;

public class ApiResponse<T>
{
    public T? Data { get; set; }
    public string? Error { get; set; }
    public bool Success { get; set; }

    public static ApiResponse<T> Ok(T data) => new() { Data = data, Success = true };
    public static ApiResponse<T> Fail(string error) => new() { Error = error, Success = false };
}

public class PagedResponse<T>
{
    public IEnumerable<T> Data { get; set; } = Enumerable.Empty<T>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public bool Success { get; set; } = true;
}
