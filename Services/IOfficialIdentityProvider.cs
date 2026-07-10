namespace ANU_Admissions.Services;

/// <summary>
/// Abstraction over the official identity source. Today this is a mock; in
/// production it will be the real university API. The contract is deliberately
/// minimal so the implementation can be swapped without touching controllers.
/// </summary>
public interface IOfficialIdentityProvider
{
    /// <summary>
    /// Looks up a person's official identity by national id.
    /// Returns null when the national id is unknown.
    /// </summary>
    Task<OfficialIdentityResult?> GetByNationalIdAsync(string nationalId);
}
