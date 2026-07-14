using System.Net;
using CodeCompass.Core.Resilience;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeCompass.Tests.Unit.Resilience;

public class RetryPolicyTests
{
    private readonly ILogger _logger = NullLogger.Instance;

    [Fact]
    public async Task ExecuteAsync_SuccessOnFirstAttempt_ReturnsResult()
    {
        var policy = new RetryPolicy(_logger);
        int callCount = 0;

        var result = await policy.ExecuteAsync<int>(ct =>
        {
            callCount++;
            return Task.FromResult(42);
        });

        result.Should().Be(42);
        callCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_VoidOperation_SuccessOnFirstAttempt()
    {
        var policy = new RetryPolicy(_logger);
        int callCount = 0;

        await policy.ExecuteAsync(ct =>
        {
            callCount++;
            return Task.CompletedTask;
        });

        callCount.Should().Be(1);
    }

    [Theory]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(504)]
    public async Task ExecuteAsync_RetryableStatusCode_RetriesAndSucceeds(int statusCode)
    {
        var policy = new RetryPolicy(
            _logger,
            maxAttempts: 3,
            baseDelay: TimeSpan.FromMilliseconds(10),
            maxDelay: TimeSpan.FromMilliseconds(100),
            multiplier: 2.0);

        int callCount = 0;

        var result = await policy.ExecuteAsync<string>(ct =>
        {
            callCount++;
            if (callCount < 2)
            {
                throw new HttpRequestException("Error", null, (HttpStatusCode)statusCode);
            }
            return Task.FromResult("success");
        });

        result.Should().Be("success");
        callCount.Should().Be(2);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(400)]
    [InlineData(404)]
    public async Task ExecuteAsync_NonRetryableStatusCode_ThrowsImmediately(int statusCode)
    {
        var policy = new RetryPolicy(
            _logger,
            maxAttempts: 3,
            baseDelay: TimeSpan.FromMilliseconds(10),
            maxDelay: TimeSpan.FromMilliseconds(100),
            multiplier: 2.0);

        int callCount = 0;

        Func<Task> act = () => policy.ExecuteAsync<string>(ct =>
        {
            callCount++;
            throw new HttpRequestException("Error", null, (HttpStatusCode)statusCode);
        });

        await act.Should().ThrowAsync<HttpRequestException>();
        callCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_ExhaustsRetries_ThrowsAfterMaxAttempts()
    {
        var policy = new RetryPolicy(
            _logger,
            maxAttempts: 3,
            baseDelay: TimeSpan.FromMilliseconds(10),
            maxDelay: TimeSpan.FromMilliseconds(100),
            multiplier: 2.0);

        int callCount = 0;

        Func<Task> act = () => policy.ExecuteAsync<string>(ct =>
        {
            callCount++;
            throw new HttpRequestException("Error", null, HttpStatusCode.TooManyRequests);
        });

        await act.Should().ThrowAsync<HttpRequestException>();
        callCount.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_SucceedsOnLastAttempt_ReturnsResult()
    {
        var policy = new RetryPolicy(
            _logger,
            maxAttempts: 3,
            baseDelay: TimeSpan.FromMilliseconds(10),
            maxDelay: TimeSpan.FromMilliseconds(100),
            multiplier: 2.0);

        int callCount = 0;

        var result = await policy.ExecuteAsync<string>(ct =>
        {
            callCount++;
            if (callCount < 3)
            {
                throw new HttpRequestException("Error", null, HttpStatusCode.ServiceUnavailable);
            }
            return Task.FromResult("finally");
        });

        result.Should().Be("finally");
        callCount.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_CancellationRequested_ThrowsOperationCanceled()
    {
        var policy = new RetryPolicy(
            _logger,
            maxAttempts: 3,
            baseDelay: TimeSpan.FromMilliseconds(1000),
            maxDelay: TimeSpan.FromMilliseconds(8000),
            multiplier: 2.0);

        using var cts = new CancellationTokenSource();
        int callCount = 0;

        Func<Task> act = () => policy.ExecuteAsync<string>(ct =>
        {
            callCount++;
            cts.Cancel();
            throw new HttpRequestException("Error", null, HttpStatusCode.TooManyRequests);
        }, cts.Token);

        await act.Should().ThrowAsync<Exception>();
        callCount.Should().Be(1);
    }

    [Theory]
    [InlineData(1, 10)]    // attempt 1: 10 * 2^0 = 10ms
    [InlineData(2, 20)]    // attempt 2: 10 * 2^1 = 20ms
    [InlineData(3, 40)]    // attempt 3: 10 * 2^2 = 40ms
    [InlineData(4, 80)]    // attempt 4: 10 * 2^3 = 80ms (capped at maxDelay)
    [InlineData(5, 80)]    // attempt 5: 10 * 2^4 = 160ms → capped at 80ms
    public void ComputeDelay_ExponentialBackoffWithCap(int attempt, double expectedMs)
    {
        var policy = new RetryPolicy(
            _logger,
            maxAttempts: 5,
            baseDelay: TimeSpan.FromMilliseconds(10),
            maxDelay: TimeSpan.FromMilliseconds(80),
            multiplier: 2.0);

        var delay = policy.ComputeDelay(attempt);

        delay.TotalMilliseconds.Should().BeApproximately(expectedMs, 0.001);
    }

    [Fact]
    public void ComputeDelay_DefaultParameters_ProducesCorrectDelays()
    {
        var policy = new RetryPolicy(_logger);

        // Default: baseDelay=1s, multiplier=2x, maxDelay=8s
        policy.ComputeDelay(1).Should().Be(TimeSpan.FromSeconds(1));  // 1 * 2^0 = 1s
        policy.ComputeDelay(2).Should().Be(TimeSpan.FromSeconds(2));  // 1 * 2^1 = 2s
        policy.ComputeDelay(3).Should().Be(TimeSpan.FromSeconds(4));  // 1 * 2^2 = 4s
        policy.ComputeDelay(4).Should().Be(TimeSpan.FromSeconds(8));  // 1 * 2^3 = 8s (max)
        policy.ComputeDelay(5).Should().Be(TimeSpan.FromSeconds(8));  // capped at 8s
    }

    [Fact]
    public void ExtractStatusCode_HttpRequestException_ReturnsStatusCode()
    {
        var ex = new HttpRequestException("Error", null, HttpStatusCode.TooManyRequests);

        var statusCode = RetryPolicy.ExtractStatusCode(ex);

        statusCode.Should().Be(429);
    }

    [Fact]
    public void ExtractStatusCode_HttpRequestExceptionWithoutStatusCode_ReturnsNull()
    {
        var ex = new HttpRequestException("Error");

        var statusCode = RetryPolicy.ExtractStatusCode(ex);

        statusCode.Should().BeNull();
    }

    [Fact]
    public void ExtractStatusCode_UnknownException_ReturnsNull()
    {
        var ex = new InvalidOperationException("Something failed");

        var statusCode = RetryPolicy.ExtractStatusCode(ex);

        statusCode.Should().BeNull();
    }

    [Fact]
    public void ExtractStatusCode_InnerHttpRequestException_ReturnsStatusCode()
    {
        var inner = new HttpRequestException("Inner", null, HttpStatusCode.BadGateway);
        var ex = new InvalidOperationException("Outer", inner);

        var statusCode = RetryPolicy.ExtractStatusCode(ex);

        statusCode.Should().Be(502);
    }

    [Fact]
    public async Task ExecuteAsync_NonHttpException_DoesNotRetry()
    {
        var policy = new RetryPolicy(
            _logger,
            maxAttempts: 3,
            baseDelay: TimeSpan.FromMilliseconds(10),
            maxDelay: TimeSpan.FromMilliseconds(100),
            multiplier: 2.0);

        int callCount = 0;

        Func<Task> act = () => policy.ExecuteAsync<string>(ct =>
        {
            callCount++;
            throw new InvalidOperationException("Not an HTTP error");
        });

        await act.Should().ThrowAsync<InvalidOperationException>();
        callCount.Should().Be(1);
    }
}
