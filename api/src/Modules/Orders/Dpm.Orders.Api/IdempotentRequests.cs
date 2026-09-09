using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dpm.Payments.Application.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Dpm.Orders.Api;

/// <summary>
/// Replay protection for money-moving endpoints (spec README A14, 03 section 3.2).
/// The client sends an Idempotency-Key; the first response for that key and
/// endpoint is stored and returned verbatim on any repeat, so a retried or
/// double-clicked checkout produces one order and one charge.
/// </summary>
public sealed class IdempotentRequests(IIdempotencyStore store)
{
    public const string HeaderName = "Idempotency-Key";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static string? ReadKey(HttpRequest request) =>
        request.Headers.TryGetValue(HeaderName, out var values) && !string.IsNullOrWhiteSpace(values)
            ? values.ToString()
            : null;

    public static string HashKey(string key) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();

    public async Task<ActionResult?> TryReplayAsync(string key, string endpoint, CancellationToken ct)
    {
        var stored = await store.FindAsync(HashKey(key), endpoint, ct);
        if (stored is null)
        {
            return null;
        }

        return new ContentResult
        {
            StatusCode = stored.StatusCode,
            ContentType = "application/json",
            Content = stored.ResponseJson,
        };
    }

    public Task RememberAsync<T>(string key, string endpoint, int statusCode, T response, CancellationToken ct) =>
        store.SaveAsync(
            HashKey(key),
            endpoint,
            statusCode,
            JsonSerializer.Serialize(response, SerializerOptions),
            ct);
}
