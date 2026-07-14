using System.Net;
using Microsoft.Extensions.Logging;

namespace CodeCompass.Core.Resilience;

/// <summary>
/// Provides retry logic with exponential backoff for external service calls.
/// Retries on transient HTTP errors (429, 500, 502, 503, 504) and immediately
/// rethrows on non-retryable errors (401, 400, 404).
/// </summary>
public class RetryPolicy
{
    private static readonly HashSet<int> RetryableStatusCodes = new()
    {
        429,  // Too Many Requests
        500,  // Internal Server Error
        502,  // Bad Gateway
        503,  // Service Unavailable
        504   // Gateway Timeout
    };

    private static readonly HashSet<int> NonRetryableStatusCodes = new()
    {
        401,  // Unauthorized
        400,  // Bad Request
        404   // Not Found
    };

    private readonly ILogger _logger;
    private readonly int _maxAttempts;
    private readonly TimeSpan _baseDelay;
    private readonly TimeSpan _maxDelay;
    private readonly double _multiplier;

    /// <summary>
    /// Initializes a new instance of <see cref="RetryPolicy"/> with default parameters:
    /// max attempts 3, base delay 1s, max delay 8s, multiplier 2x.
    /// </summary>
    public RetryPolicy(ILogger logger)
        : this(logger, maxAttempts: 3, baseDelay: TimeSpan.FromSeconds(1), maxDelay: TimeSpan.FromSeconds(8), multiplier: 2.0)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="RetryPolicy"/> with custom parameters.
    /// </summary>
    public RetryPolicy(ILogger logger, int maxAttempts, TimeSpan baseDelay, TimeSpan maxDelay, double multiplier)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _maxAttempts = maxAttempts;
        _baseDelay = baseDelay;
        _maxDelay = maxDelay;
        _multiplier = multiplier;
    }

    /// <summary>
    /// Executes an async operation with retry logic, returning a result.
    /// </summary>
    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        int attempt = 0;

        while (true)
        {
            try
            {
                attempt++;
                return await operation(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                int? statusCode = ExtractStatusCode(ex);

                if (statusCode.HasValue && NonRetryableStatusCodes.Contains(statusCode.Value))
                {
                    _logger.LogWarning(
                        "Non-retryable error (HTTP {StatusCode}) encountered. Not retrying.",
                        statusCode.Value);
                    throw;
                }

                bool isRetryable = statusCode.HasValue && RetryableStatusCodes.Contains(statusCode.Value);

                if (!isRetryable || attempt >= _maxAttempts)
                {
                    if (isRetryable)
                    {
                        _logger.LogError(
                            ex,
                            "Retry attempts exhausted after {Attempts} attempts. Last error: HTTP {StatusCode}.",
                            attempt,
                            statusCode);
                    }
                    throw;
                }

                TimeSpan delay = ComputeDelay(attempt);
                _logger.LogWarning(
                    "Retryable error (HTTP {StatusCode}) on attempt {Attempt}/{MaxAttempts}. Retrying in {Delay}ms.",
                    statusCode!.Value,
                    attempt,
                    _maxAttempts,
                    (int)delay.TotalMilliseconds);

                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Executes an async operation with retry logic for void operations.
    /// </summary>
    public async Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
    {
        await ExecuteAsync<object?>(async ct =>
        {
            await operation(ct).ConfigureAwait(false);
            return null;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Computes the delay for the given attempt using exponential backoff,
    /// capped at the configured max delay.
    /// delay = min(baseDelay * multiplier^(attempt-1), maxDelay)
    /// </summary>
    internal TimeSpan ComputeDelay(int attempt)
    {
        double delayMs = _baseDelay.TotalMilliseconds * Math.Pow(_multiplier, attempt - 1);
        double cappedMs = Math.Min(delayMs, _maxDelay.TotalMilliseconds);
        return TimeSpan.FromMilliseconds(cappedMs);
    }

    /// <summary>
    /// Extracts an HTTP status code from known exception types.
    /// Supports HttpRequestException and Azure SDK RequestFailedException patterns.
    /// </summary>
    internal static int? ExtractStatusCode(Exception exception)
    {
        // .NET HttpRequestException (net5+ has StatusCode property)
        if (exception is HttpRequestException httpEx && httpEx.StatusCode is HttpStatusCode statusCode)
        {
            return (int)statusCode;
        }

        // Azure SDK RequestFailedException - uses reflection to avoid hard dependency
        // The Azure.Core library's RequestFailedException has a Status property (int)
        var exType = exception.GetType();
        if (exType.Name == "RequestFailedException" || exType.BaseType?.Name == "RequestFailedException")
        {
            var statusProperty = exType.GetProperty("Status");
            if (statusProperty != null && statusProperty.PropertyType == typeof(int))
            {
                var statusValue = statusProperty.GetValue(exception);
                if (statusValue is int status)
                {
                    return status;
                }
            }
        }

        // Check inner exception
        if (exception.InnerException != null)
        {
            return ExtractStatusCode(exception.InnerException);
        }

        return null;
    }
}
