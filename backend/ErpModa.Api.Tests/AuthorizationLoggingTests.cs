using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ErpModa.Api.Tests;

public sealed class CapturingLoggerProvider : ILoggerProvider
{
    public ConcurrentQueue<(string Category, LogLevel Level, string Message)> Entries { get; } = new();

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, Entries);

    public void Dispose() { }

    private sealed class CapturingLogger : ILogger
    {
        private readonly string _category;
        private readonly ConcurrentQueue<(string, LogLevel, string)> _entries;

        public CapturingLogger(string category, ConcurrentQueue<(string, LogLevel, string)> entries)
        {
            _category = category;
            _entries = entries;
        }

        public IDisposable BeginScope<TState>(TState state) => new NoopDisposable();

        private sealed class NoopDisposable : IDisposable { public void Dispose() { } }
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => _entries.Enqueue((_category, logLevel, formatter(state, exception)));
    }
}

public class AuthorizationLoggingTests
{
    [Fact]
    public async Task SinToken_GeneraUnSoloEvento401ConCorrelationId()
    {
        var loggerProvider = new CapturingLoggerProvider();
        var factory = new AuthTestFactory();
        var clientFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddSingleton<ILoggerProvider>(loggerProvider)));
        var client = clientFactory.CreateClient();

        var correlationId = "corr-401-test";
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/registro");
        request.Headers.Add("X-Correlation-ID", correlationId);
        request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");

        var before = loggerProvider.Entries.Count;
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var nuevas = loggerProvider.Entries.Skip(before).ToList();
        var eventos401 = nuevas.Where(e => e.Message.Contains("401") && e.Message.Contains(correlationId)).ToList();
        Assert.Single(eventos401);
    }

    [Fact]
    public async Task RolInsuficiente_GeneraUnSoloEvento403ConCorrelationId()
    {
        var loggerProvider = new CapturingLoggerProvider();
        var factory = new AuthTestFactory();
        var clientFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddSingleton<ILoggerProvider>(loggerProvider)));
        var client = clientFactory.CreateClient();

        var token = factory.CrearToken(1, "Vendedor");
        var correlationId = "corr-403-test";
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/registro");
        request.Headers.Add("X-Correlation-ID", correlationId);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");

        var before = loggerProvider.Entries.Count;
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var nuevas = loggerProvider.Entries.Skip(before).ToList();
        var eventos403 = nuevas.Where(e => e.Message.Contains("403") && e.Message.Contains(correlationId)).ToList();
        Assert.Single(eventos403);
    }
}
