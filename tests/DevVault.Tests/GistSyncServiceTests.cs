using DevSnippetManager.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DevVault.Tests;

public class GistSyncServiceTests
{
    private const string SecretToken = "ghp_SUPER_SECRET_TOKEN_DO_NOT_LEAK";

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public List<HttpRequestMessage> Requests { get; } = new();
        public List<string?> RequestBodies { get; } = new();

        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            RequestBodies.Add(request.Content != null ? await request.Content.ReadAsStringAsync(cancellationToken) : null);
            return _responder(request);
        }
    }

    [Fact]
    public async Task PushAsync_CreatesNewGistWithPostAndRequiredHeaders()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = new StringContent("""{"id":"gist123","updated_at":"2026-11-21T18:00:00Z"}""", Encoding.UTF8, "application/json")
        });
        var service = new GistSyncService(new HttpClient(handler));

        var result = await service.PushAsync(SecretToken, null, "[]");

        Assert.True(result.Success);
        Assert.Equal(GistStatus.Ok, result.Status);
        Assert.Equal("gist123", result.GistId);
        Assert.DoesNotContain(SecretToken, result.Message);

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.Equal("https://api.github.com/gists", req.RequestUri!.AbsoluteUri);
        Assert.Equal("Bearer " + SecretToken, req.Headers.GetValues("Authorization").Single());
        Assert.Equal("2022-11-28", req.Headers.GetValues("X-GitHub-Api-Version").Single());
        Assert.Contains("DevVault", req.Headers.UserAgent.ToString());

        var body = handler.RequestBodies[0]!;
        Assert.Contains("\"public\":false", body);
        Assert.Contains($"\"{GistSyncService.FileName}\"", body);
    }

    [Fact]
    public async Task PushAsync_UpdatesExistingGistWithPatch()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":"abc","updated_at":"2026-11-21T19:00:00Z"}""", Encoding.UTF8, "application/json")
        });
        var service = new GistSyncService(new HttpClient(handler));

        var result = await service.PushAsync(SecretToken, "abc", "[]");

        Assert.True(result.Success);
        Assert.Equal(HttpMethod.Patch, handler.Requests[0].Method);
        Assert.Equal("https://api.github.com/gists/abc", handler.Requests[0].RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task PullAsync_FetchesRawUrlWhenTruncated()
    {
        var handler = new FakeHandler(req =>
        {
            if (req.RequestUri!.AbsoluteUri == "https://api.github.com/gists/abc")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"id":"abc","updated_at":"2026-11-21T19:00:00Z","files":{"devvault-snippets.json":{"truncated":true,"raw_url":"https://gist.githubusercontent.com/raw/abc"}}}""",
                        Encoding.UTF8,
                        "application/json")
                };
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""[{"Title":"FromRaw"}]""", Encoding.UTF8, "application/json")
            };
        });

        var service = new GistSyncService(new HttpClient(handler));
        var result = await service.PullAsync(SecretToken, "abc");

        Assert.True(result.Success);
        Assert.Equal("""[{"Title":"FromRaw"}]""", result.Content);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("Bearer " + SecretToken, handler.Requests[1].Headers.GetValues("Authorization").Single());
    }

    [Theory]
    [InlineData("https://evil.example/steal")]
    [InlineData("http://gist.githubusercontent.com/raw/abc")]
    public async Task PullAsync_RefusesForeignOrNonHttpsRawUrlWithoutSendingRequest(string badRawUrl)
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"id\":\"abc\",\"files\":{\"devvault-snippets.json\":{\"truncated\":true,\"raw_url\":\"" + badRawUrl + "\"}}}",
                Encoding.UTF8,
                "application/json")
        });

        var service = new GistSyncService(new HttpClient(handler));
        var result = await service.PullAsync(SecretToken, "abc");

        Assert.False(result.Success);
        Assert.Equal(GistStatus.InvalidResponse, result.Status);
        Assert.Equal("Beklenmeyen gist içerik adresi.", result.Message);
        Assert.Single(handler.Requests); // Never called the foreign host
    }

    [Fact]
    public async Task ValidateTokenAsync_ChecksOAuthScopesAndReturnsLogin()
    {
        // Classic token without gist scope -> rejected
        var noGistHandler = new FakeHandler(_ =>
        {
            var resp = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"login":"alperen"}""", Encoding.UTF8, "application/json")
            };
            resp.Headers.Add("X-OAuth-Scopes", "repo, read:user");
            return resp;
        });
        var noGist = await new GistSyncService(new HttpClient(noGistHandler)).ValidateTokenAsync(SecretToken);
        Assert.False(noGist.Success);
        Assert.Equal(GistStatus.Unauthorized, noGist.Status);

        // Classic token with gist scope -> accepted
        var withGistHandler = new FakeHandler(_ =>
        {
            var resp = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"login":"alperen"}""", Encoding.UTF8, "application/json")
            };
            resp.Headers.Add("X-OAuth-Scopes", "repo, gist");
            return resp;
        });
        var withGist = await new GistSyncService(new HttpClient(withGistHandler)).ValidateTokenAsync(SecretToken);
        Assert.True(withGist.Success);
        Assert.Equal("alperen", withGist.Message);

        // Fine-grained token (no X-OAuth-Scopes header) -> accepted
        var fineGrainedHandler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"login":"fineuser"}""", Encoding.UTF8, "application/json")
        });
        var fine = await new GistSyncService(new HttpClient(fineGrainedHandler)).ValidateTokenAsync(SecretToken);
        Assert.True(fine.Success);
        Assert.Equal("fineuser", fine.Message);
    }

    [Fact]
    public async Task AdditionalGistErrors_Patch404_429_TruncatedNoRawUrl_InvalidJson_Timeout_Cancellation()
    {
        // PATCH 404
        var patch404 = await new GistSyncService(new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound))))
            .PushAsync(SecretToken, "missing", "[]");
        Assert.Equal(GistStatus.NotFound, patch404.Status);

        // 429 TooManyRequests
        var rate429 = await new GistSyncService(new HttpClient(new FakeHandler(_ => new HttpResponseMessage((HttpStatusCode)429))))
            .PullAsync(SecretToken, "abc");
        Assert.Equal(GistStatus.RateLimited, rate429.Status);

        // Truncated without content or raw_url
        var truncNoRaw = await new GistSyncService(new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":"abc","files":{"devvault-snippets.json":{"truncated":true}}}""", Encoding.UTF8, "application/json")
        }))).PullAsync(SecretToken, "abc");
        Assert.Equal(GistStatus.InvalidResponse, truncNoRaw.Status);

        // Invalid JSON
        var badJson = await new GistSyncService(new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not-json", Encoding.UTF8, "application/json")
        }))).PullAsync(SecretToken, "abc");
        Assert.Equal(GistStatus.InvalidResponse, badJson.Status);

        // HttpClient timeout (TaskCanceledException when caller token NOT cancelled) -> NetworkError
        var timeoutResult = await new GistSyncService(new HttpClient(new FakeHandler(_ => throw new TaskCanceledException("timeout"))))
            .ValidateTokenAsync(SecretToken);
        Assert.Equal(GistStatus.NetworkError, timeoutResult.Status);

        // Caller cancellation rethrows OperationCanceledException
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new GistSyncService(new HttpClient(new FakeHandler(_ => throw new OperationCanceledException(cts.Token))))
                .ValidateTokenAsync(SecretToken, cts.Token));
    }

    [Fact]
    public async Task PullAsync_ReturnsInvalidResponseWhenExpectedFileMissing()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":"abc","files":{"other.txt":{"content":"hi"}}}""", Encoding.UTF8, "application/json")
        });
        var service = new GistSyncService(new HttpClient(handler));

        var result = await service.PullAsync(SecretToken, "abc");

        Assert.False(result.Success);
        Assert.Equal(GistStatus.InvalidResponse, result.Status);
        Assert.DoesNotContain(SecretToken, result.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, false, GistStatus.NotFound)]
    [InlineData(HttpStatusCode.Unauthorized, false, GistStatus.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, true, GistStatus.RateLimited)]
    [InlineData(HttpStatusCode.Forbidden, false, GistStatus.Unauthorized)]
    public async Task ErrorStatuses_MapCleanlyAndNeverLeakToken(HttpStatusCode code, bool rateLimitZero, GistStatus expectedStatus)
    {
        var handler = new FakeHandler(_ =>
        {
            var resp = new HttpResponseMessage(code) { Content = new StringContent(SecretToken) };
            if (rateLimitZero)
                resp.Headers.Add("X-RateLimit-Remaining", "0");
            return resp;
        });

        var service = new GistSyncService(new HttpClient(handler));
        var result = await service.PullAsync(SecretToken, "abc");

        Assert.False(result.Success);
        Assert.Equal(expectedStatus, result.Status);
        Assert.DoesNotContain(SecretToken, result.Message);
    }

    [Fact]
    public async Task NetworkError_ReturnsNetworkErrorWithoutLeakingToken()
    {
        var handler = new FakeHandler(_ => throw new HttpRequestException($"Failed with {SecretToken}"));
        var service = new GistSyncService(new HttpClient(handler));

        var result = await service.ValidateTokenAsync(SecretToken);

        Assert.False(result.Success);
        Assert.Equal(GistStatus.NetworkError, result.Status);
        Assert.DoesNotContain(SecretToken, result.Message);
    }
}
