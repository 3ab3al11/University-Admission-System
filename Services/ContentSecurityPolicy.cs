using System.Security.Cryptography;

namespace ANU_Admissions.Services;

public static class ContentSecurityPolicy
{
    public const string NonceItemKey = "ANU.ContentSecurityPolicy.Nonce";

    public static string CreateNonce()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    }

    public static string BuildHeader(string nonce, bool requireHttps)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nonce);

        var policy =
            "default-src 'self'; " +
            $"script-src 'self' 'nonce-{nonce}'; " +
            "script-src-attr 'none'; " +
            $"style-src 'self' 'nonce-{nonce}' https://cdn.jsdelivr.net; " +
            $"style-src-elem 'self' 'nonce-{nonce}' https://cdn.jsdelivr.net; " +
            "style-src-attr 'unsafe-inline'; " +
            "font-src 'self' https://cdn.jsdelivr.net; " +
            "img-src 'self' data:; " +
            "connect-src 'self'; " +
            "worker-src 'self'; " +
            "frame-ancestors 'none'; " +
            "object-src 'none'; " +
            "base-uri 'self'; " +
            "form-action 'self'; " +
            "manifest-src 'self';";

        return requireHttps
            ? policy + " upgrade-insecure-requests; block-all-mixed-content;"
            : policy;
    }
}
