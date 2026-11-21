using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace DevSnippetManager.Services;

public sealed class GistSyncService
{
    public const string FileName = "devvault-snippets.json";

    private const string ApiBase = "https://api.github.com/";
    private const string Description = "DevVault snippet yedeği";
    private readonly HttpClient http;

    public GistSyncService(HttpClient http)
    {
        this.http = http ?? throw new ArgumentNullException(nameof(http));
    }

    public async Task<GistResult> PushAsync(string token, string? gistId, string json, CancellationToken ct = default)
    {
        bool create = string.IsNullOrWhiteSpace(gistId);
        string uri = create ? ApiBase + "gists" : ApiBase + "gists/" + Uri.EscapeDataString(gistId!);
        JsonObject files = new()
        {
            [FileName] = new JsonObject { ["content"] = json }
        };
        JsonObject payload = new()
        {
            ["description"] = Description,
            ["public"] = false,
            ["files"] = files
        };

        HttpExchange exchange = await SendAsync(create ? HttpMethod.Post : HttpMethod.Patch, uri, token, payload.ToJsonString(), ct).ConfigureAwait(false);
        if (!exchange.IsSuccessStatusCode)
        {
            return MapFailure(exchange);
        }

        try
        {
            JsonObject root = JsonNode.Parse(exchange.Body)?.AsObject()
                ?? throw new JsonException();
            string? id = root["id"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(id))
            {
                return InvalidResponse();
            }

            return new GistResult(true, GistStatus.Ok, id, null, create ? "Gist oluşturuldu." : "Gist güncellendi.", ReadUpdatedAt(root));
        }
        catch (JsonException)
        {
            return InvalidResponse();
        }
        catch (InvalidOperationException)
        {
            return InvalidResponse();
        }
    }

    public async Task<GistResult> PullAsync(string token, string gistId, CancellationToken ct = default)
    {
        HttpExchange exchange = await SendAsync(HttpMethod.Get, ApiBase + "gists/" + Uri.EscapeDataString(gistId), token, null, ct).ConfigureAwait(false);
        if (!exchange.IsSuccessStatusCode)
        {
            return MapFailure(exchange, gistId);
        }

        try
        {
            JsonObject root = JsonNode.Parse(exchange.Body)?.AsObject()
                ?? throw new JsonException();
            string? id = root["id"]?.GetValue<string>();
            DateTimeOffset? updatedAt = ReadUpdatedAt(root);
            JsonObject? files = root["files"]?.AsObject();
            JsonObject? file = files?[FileName]?.AsObject();
            if (file is null)
            {
                return new GistResult(false, GistStatus.InvalidResponse, id, null, "Gist içinde devvault-snippets.json bulunamadı.", updatedAt);
            }

            string? content = file["content"]?.GetValue<string>();
            bool truncated = file["truncated"]?.GetValue<bool>() ?? false;
            if (!truncated && content is not null)
            {
                return new GistResult(true, GistStatus.Ok, id, content, "Gist içeriği alındı.", updatedAt);
            }

            string? rawUrl = file["raw_url"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(rawUrl))
            {
                return InvalidResponse(id, updatedAt);
            }

            if (!IsAllowedRawUrl(rawUrl))
            {
                return new GistResult(false, GistStatus.InvalidResponse, id, null, "Beklenmeyen gist içerik adresi.", updatedAt);
            }

            HttpExchange rawExchange = await SendAsync(HttpMethod.Get, rawUrl, token, null, ct).ConfigureAwait(false);
            if (!rawExchange.IsSuccessStatusCode)
            {
                return MapFailure(rawExchange, id, updatedAt);
            }

            return new GistResult(true, GistStatus.Ok, id, rawExchange.Body, "Gist içeriği alındı.", updatedAt);
        }
        catch (JsonException)
        {
            return InvalidResponse();
        }
        catch (InvalidOperationException)
        {
            return InvalidResponse();
        }
    }

    public async Task<GistResult> ValidateTokenAsync(string token, CancellationToken ct = default)
    {
        HttpExchange exchange = await SendAsync(HttpMethod.Get, ApiBase + "user", token, null, ct).ConfigureAwait(false);
        if (!exchange.IsSuccessStatusCode)
        {
            return MapFailure(exchange);
        }

        if (exchange.OAuthScopes is not null && !HasGistScope(exchange.OAuthScopes))
        {
            return new GistResult(false, GistStatus.Unauthorized, null, null, "Token'ın gist izni yok (gist kapsamı gerekli).", null);
        }

        try
        {
            JsonObject root = JsonNode.Parse(exchange.Body)?.AsObject()
                ?? throw new JsonException();
            string? login = root["login"]?.GetValue<string>();
            return string.IsNullOrWhiteSpace(login)
                ? InvalidResponse()
                : new GistResult(true, GistStatus.Ok, null, null, login, null);
        }
        catch (JsonException)
        {
            return InvalidResponse();
        }
        catch (InvalidOperationException)
        {
            return InvalidResponse();
        }
    }

    private async Task<HttpExchange> SendAsync(HttpMethod method, string uri, string token, string? body, CancellationToken ct)
    {
        using HttpRequestMessage request = new(method, uri);
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + (token ?? string.Empty));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
        request.Headers.UserAgent.ParseAdd("DevVault");
        if (body is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        try
        {
            using HttpResponseMessage response = await http.SendAsync(request, ct).ConfigureAwait(false);
            string responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            bool rateLimitRemainingZero = response.Headers.TryGetValues("X-RateLimit-Remaining", out IEnumerable<string>? values) &&
                string.Equals(values.FirstOrDefault(), "0", StringComparison.Ordinal);
            string? oauthScopes = response.Headers.TryGetValues("X-OAuth-Scopes", out IEnumerable<string>? scopeValues)
                ? string.Join(",", scopeValues)
                : null;
            return new HttpExchange(response.StatusCode, responseBody, rateLimitRemainingZero, oauthScopes);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            return new HttpExchange(HttpStatusCode.ServiceUnavailable, string.Empty, false, null, ex);
        }
        catch (TaskCanceledException ex)
        {
            return new HttpExchange(HttpStatusCode.ServiceUnavailable, string.Empty, false, null, ex);
        }
        catch (OperationCanceledException ex)
        {
            return new HttpExchange(HttpStatusCode.ServiceUnavailable, string.Empty, false, null, ex);
        }
    }

    private static bool IsAllowedRawUrl(string rawUrl)
    {
        if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri))
            return false;
        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return false;

        var host = uri.Host;
        return string.Equals(host, "gist.githubusercontent.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "api.github.com", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasGistScope(string scopesHeader)
    {
        return scopesHeader
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Any(s => string.Equals(s.Trim(), "gist", StringComparison.OrdinalIgnoreCase));
    }

    private static GistResult MapFailure(HttpExchange exchange, string? gistId = null, DateTimeOffset? updatedAt = null)
    {
        if (exchange.Exception is not null)
        {
            return new GistResult(false, GistStatus.NetworkError, gistId, null, "GitHub'a bağlanılamadı.", updatedAt);
        }

        if (exchange.StatusCode == HttpStatusCode.Unauthorized)
        {
            return new GistResult(false, GistStatus.Unauthorized, gistId, null, "GitHub token geçersiz veya süresi dolmuş.", updatedAt);
        }

        bool rateLimited = exchange.StatusCode == (HttpStatusCode)429 ||
            (exchange.StatusCode == HttpStatusCode.Forbidden && exchange.RateLimitRemainingZero);
        if (rateLimited)
        {
            return new GistResult(false, GistStatus.RateLimited, gistId, null, "GitHub istek sınırı aşıldı, daha sonra tekrar deneyin.", updatedAt);
        }

        if (exchange.StatusCode == HttpStatusCode.Forbidden)
        {
            return new GistResult(false, GistStatus.Unauthorized, gistId, null, "Token'ın gist izni yok (gist kapsamı gerekli).", updatedAt);
        }

        if (exchange.StatusCode == HttpStatusCode.NotFound)
        {
            return new GistResult(false, GistStatus.NotFound, gistId, null, "Gist bulunamadı.", updatedAt);
        }

        return new GistResult(false, GistStatus.Error, gistId, null, "GitHub isteği başarısız oldu.", updatedAt);
    }

    private static DateTimeOffset? ReadUpdatedAt(JsonObject root)
    {
        string? value = root["updated_at"]?.GetValue<string>();
        return DateTimeOffset.TryParse(value, out DateTimeOffset result) ? result : null;
    }

    private static GistResult InvalidResponse(string? gistId = null, DateTimeOffset? updatedAt = null) =>
        new(false, GistStatus.InvalidResponse, gistId, null, "GitHub'dan geçersiz yanıt alındı.", updatedAt);

    private sealed record HttpExchange(HttpStatusCode StatusCode, string Body, bool RateLimitRemainingZero, string? OAuthScopes = null, Exception? Exception = null)
    {
        public bool IsSuccessStatusCode => (int)StatusCode is >= 200 and <= 299;
    }
}

public sealed record GistResult(bool Success, GistStatus Status, string? GistId, string? Content, string Message, DateTimeOffset? UpdatedAt);

public enum GistStatus
{
    Ok,
    Unauthorized,
    NotFound,
    RateLimited,
    NetworkError,
    InvalidResponse,
    Error
}
