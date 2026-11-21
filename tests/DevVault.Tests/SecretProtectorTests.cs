using DevSnippetManager.Services;
using Xunit;

namespace DevVault.Tests;

public class SecretProtectorTests
{
    [Fact]
    public void ProtectAndUnprotect_RoundTripsSecret()
    {
        const string secret = "ghp_ExampleSecretToken1234567890";
        var protectedValue = SecretProtector.Protect(secret);

        Assert.NotEqual(secret, protectedValue);
        Assert.False(string.IsNullOrWhiteSpace(protectedValue));
        Assert.Equal(secret, SecretProtector.Unprotect(protectedValue));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-valid-base64!!!")]
    [InlineData("AQIDBAUGBwgJCgsMDQ4PEA==")]
    public void Unprotect_ReturnsEmptyForEmptyOrGarbage(string input)
    {
        Assert.Equal(string.Empty, SecretProtector.Unprotect(input));
    }

    [Fact]
    public void Protect_EmptyReturnsEmpty()
    {
        Assert.Equal(string.Empty, SecretProtector.Protect(string.Empty));
    }
}
