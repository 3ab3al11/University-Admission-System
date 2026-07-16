using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace ANU_Admissions.Services;

public sealed class ApplicationUrlOptions
{
    public const string SectionName = "ApplicationUrls";

    public string PublicBaseUrl { get; set; } = string.Empty;
}

public static class PublicUrlRules
{
    public static bool IsValidBaseUrl(string? value, bool requireHttps)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var validScheme = uri.Scheme == Uri.UriSchemeHttps
            || (!requireHttps && uri.Scheme == Uri.UriSchemeHttp);

        return validScheme
            && !string.IsNullOrWhiteSpace(uri.Host)
            && string.IsNullOrEmpty(uri.UserInfo)
            && string.IsNullOrEmpty(uri.Query)
            && string.IsNullOrEmpty(uri.Fragment)
            && uri.AbsolutePath == "/";
    }
}

public interface IPasswordResetLinkFactory
{
    string Create(string email, string encodedToken);
}

/// <summary>
/// Builds password-reset links from a trusted configured origin instead of the
/// request Host header, preventing poisoned reset links from reaching users.
/// </summary>
public sealed class PasswordResetLinkFactory : IPasswordResetLinkFactory
{
    private readonly Uri _resetEndpoint;

    public PasswordResetLinkFactory(IOptions<ApplicationUrlOptions> options)
    {
        var publicBaseUrl = options.Value.PublicBaseUrl;
        if (!PublicUrlRules.IsValidBaseUrl(publicBaseUrl, requireHttps: false))
        {
            throw new InvalidOperationException(
                $"{ApplicationUrlOptions.SectionName}:PublicBaseUrl is invalid.");
        }

        _resetEndpoint = new Uri(new Uri(publicBaseUrl), "/Account/ResetPassword");
    }

    public string Create(string email, string encodedToken) =>
        QueryHelpers.AddQueryString(
            _resetEndpoint.ToString(),
            new Dictionary<string, string?>
            {
                ["email"] = email,
                ["token"] = encodedToken
            });
}
