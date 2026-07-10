namespace ANU_Admissions.Services;

/// <summary>
/// Graduation-project stand-in for the real university identity API.
///
/// Maps a national id to the person's identity and OFFICIAL seat number.
/// The seat numbers below correspond to sample rows in OfficialStudentRecords
/// imported during local testing, so the end-to-end flow can
/// be tested:
///   - 100000/100001/100002 are eligible (ناجح).
///   - 100326 is ineligible (راسب) — used to exercise the not-eligible path.
///
/// Replace this with an HTTP-backed implementation of IOfficialIdentityProvider
/// when the university API is available; no controller changes are required.
/// </summary>
public class MockOfficialIdentityProvider : IOfficialIdentityProvider
{
    private static readonly Dictionary<string, OfficialIdentityResult> Data = new()
    {
        ["30101011234567"] = new OfficialIdentityResult
        {
            NationalId = "30101011234567",
            FullName = "طالب تجريبي 001",
            FatherName = "ولي أمر تجريبي",
            BirthDate = new DateTime(2001, 1, 1),
            SeatNumber = "100000"
        },
        ["30102021234567"] = new OfficialIdentityResult
        {
            NationalId = "30102021234567",
            FullName = "طالب تجريبي 002",
            FatherName = "ولي أمر تجريبي",
            BirthDate = new DateTime(2001, 2, 2),
            SeatNumber = "100001"
        },
        ["30103031234567"] = new OfficialIdentityResult
        {
            NationalId = "30103031234567",
            FullName = "طالب تجريبي 003",
            FatherName = "ولي أمر تجريبي",
            BirthDate = new DateTime(2001, 3, 3),
            SeatNumber = "100002"
        },
        // Points at an INELIGIBLE official record (راسب) for testing.
        ["30104041234567"] = new OfficialIdentityResult
        {
            NationalId = "30104041234567",
            FullName = "طالب تجريبي 004",
            FatherName = "ولي أمر تجريبي",
            BirthDate = new DateTime(2001, 4, 4),
            SeatNumber = "100326"
        },
        // A DIFFERENT national id that points at seat 100000 — lets us test the
        // "seat already linked to another account" path with a distinct id.
        ["30105051234567"] = new OfficialIdentityResult
        {
            NationalId = "30105051234567",
            FullName = "طالب تجريبي 005",
            FatherName = "ولي أمر تجريبي",
            BirthDate = new DateTime(2001, 5, 5),
            SeatNumber = "100000"
        },

        // -----------------------------------------------------------------
        // Ready-to-use test identities — 30 eligible students spread from
        // ~95% down to ~55%. National id = "30000000" + seat number.
        // Each seat links to ONE account only.
        // -----------------------------------------------------------------
        ["30000000100071"] = new OfficialIdentityResult { NationalId = "30000000100071", FullName = "طالب تجريبي 006", SeatNumber = "100071" },  // %95.27
        ["30000000100059"] = new OfficialIdentityResult { NationalId = "30000000100059", FullName = "طالب تجريبي 007", SeatNumber = "100059" },  // %94.07
        ["30000000100011"] = new OfficialIdentityResult { NationalId = "30000000100011", FullName = "طالب تجريبي 008", SeatNumber = "100011" },  // %93.08
        ["30000000100040"] = new OfficialIdentityResult { NationalId = "30000000100040", FullName = "طالب تجريبي 009", SeatNumber = "100040" },  // %91.79
        ["30000000100034"] = new OfficialIdentityResult { NationalId = "30000000100034", FullName = "طالب تجريبي 010", SeatNumber = "100034" },  // %90.08
        ["30000000100014"] = new OfficialIdentityResult { NationalId = "30000000100014", FullName = "طالب تجريبي 011", SeatNumber = "100014" },  // %89.01
        ["30000000100006"] = new OfficialIdentityResult { NationalId = "30000000100006", FullName = "طالب تجريبي 012", SeatNumber = "100006" },  // %88.54
        ["30000000100005"] = new OfficialIdentityResult { NationalId = "30000000100005", FullName = "طالب تجريبي 013", SeatNumber = "100005" },  // %86.04
        ["30000000100009"] = new OfficialIdentityResult { NationalId = "30000000100009", FullName = "طالب تجريبي 014", SeatNumber = "100009" },  // %85.59
        ["30000000100008"] = new OfficialIdentityResult { NationalId = "30000000100008", FullName = "طالب تجريبي 015", SeatNumber = "100008" },  // %84.27
        ["30000000100007"] = new OfficialIdentityResult { NationalId = "30000000100007", FullName = "طالب تجريبي 016", SeatNumber = "100007" },  // %83.30
        ["30000000100036"] = new OfficialIdentityResult { NationalId = "30000000100036", FullName = "طالب تجريبي 017", SeatNumber = "100036" },  // %81.36
        ["30000000100024"] = new OfficialIdentityResult { NationalId = "30000000100024", FullName = "طالب تجريبي 018", SeatNumber = "100024" },  // %80.72
        ["30000000100029"] = new OfficialIdentityResult { NationalId = "30000000100029", FullName = "طالب تجريبي 019", SeatNumber = "100029" },  // %79.64
        ["30000000100035"] = new OfficialIdentityResult { NationalId = "30000000100035", FullName = "طالب تجريبي 020", SeatNumber = "100035" },  // %78.67
        ["30000000100117"] = new OfficialIdentityResult { NationalId = "30000000100117", FullName = "طالب تجريبي 021", SeatNumber = "100117" },  // %76.99
        ["30000000100010"] = new OfficialIdentityResult { NationalId = "30000000100010", FullName = "طالب تجريبي 022", SeatNumber = "100010" },  // %75.89
        ["30000000100300"] = new OfficialIdentityResult { NationalId = "30000000100300", FullName = "طالب تجريبي 023", SeatNumber = "100300" },  // %74.04
        ["30000000100041"] = new OfficialIdentityResult { NationalId = "30000000100041", FullName = "طالب تجريبي 024", SeatNumber = "100041" },  // %73.95
        ["30000000100327"] = new OfficialIdentityResult { NationalId = "30000000100327", FullName = "طالب تجريبي 025", SeatNumber = "100327" },  // %71.56
        ["30000000100455"] = new OfficialIdentityResult { NationalId = "30000000100455", FullName = "طالب تجريبي 026", SeatNumber = "100455" },  // %70.10
        ["30000000100113"] = new OfficialIdentityResult { NationalId = "30000000100113", FullName = "طالب تجريبي 027", SeatNumber = "100113" },  // %69.92
        ["30000000100331"] = new OfficialIdentityResult { NationalId = "30000000100331", FullName = "طالب تجريبي 028", SeatNumber = "100331" },  // %68.50
        ["30000000101326"] = new OfficialIdentityResult { NationalId = "30000000101326", FullName = "طالب تجريبي 029", SeatNumber = "101326" },  // %66.61
        ["30000000101509"] = new OfficialIdentityResult { NationalId = "30000000101509", FullName = "طالب تجريبي 030", SeatNumber = "101509" },  // %65.23
        ["30000000100932"] = new OfficialIdentityResult { NationalId = "30000000100932", FullName = "طالب تجريبي 031", SeatNumber = "100932" },  // %64.74
        ["30000000101241"] = new OfficialIdentityResult { NationalId = "30000000101241", FullName = "طالب تجريبي 032", SeatNumber = "101241" },  // %62.31
        ["30000000102285"] = new OfficialIdentityResult { NationalId = "30000000102285", FullName = "طالب تجريبي 033", SeatNumber = "102285" },  // %57.29
        ["30000000101818"] = new OfficialIdentityResult { NationalId = "30000000101818", FullName = "طالب تجريبي 034", SeatNumber = "101818" },  // %56.00
        ["30000000101779"] = new OfficialIdentityResult { NationalId = "30000000101779", FullName = "طالب تجريبي 035", SeatNumber = "101779" },  // %55.43

        // -----------------------------------------------------------------
        // Additional 41 identities sourced from OfficialStudentRecords (DB):
        // seat 100323 (user-requested) + 40 others spanning ~95% down to ~65%.
        // Percentages match the DB (computed at MaxScore = 700).
        // -----------------------------------------------------------------
        ["30000000100323"] = new OfficialIdentityResult { NationalId = "30000000100323", FullName = "طالب تجريبي 036", SeatNumber = "100323" },  // %89.35
        ["30000000101275"] = new OfficialIdentityResult { NationalId = "30000000101275", FullName = "طالب تجريبي 037", SeatNumber = "101275" },  // %95.28
        ["30000000100710"] = new OfficialIdentityResult { NationalId = "30000000100710", FullName = "طالب تجريبي 038", SeatNumber = "100710" },  // %92.75
        ["30000000100245"] = new OfficialIdentityResult { NationalId = "30000000100245", FullName = "طالب تجريبي 039", SeatNumber = "100245" },  // %91.63
        ["30000000100968"] = new OfficialIdentityResult { NationalId = "30000000100968", FullName = "طالب تجريبي 040", SeatNumber = "100968" },  // %90.83
        ["30000000100355"] = new OfficialIdentityResult { NationalId = "30000000100355", FullName = "طالب تجريبي 041", SeatNumber = "100355" },  // %90.14
        ["30000000100874"] = new OfficialIdentityResult { NationalId = "30000000100874", FullName = "طالب تجريبي 042", SeatNumber = "100874" },  // %89.72
        ["30000000101398"] = new OfficialIdentityResult { NationalId = "30000000101398", FullName = "طالب تجريبي 043", SeatNumber = "101398" },  // %89.30
        ["30000000100967"] = new OfficialIdentityResult { NationalId = "30000000100967", FullName = "طالب تجريبي 044", SeatNumber = "100967" },  // %88.84
        ["30000000100213"] = new OfficialIdentityResult { NationalId = "30000000100213", FullName = "طالب تجريبي 045", SeatNumber = "100213" },  // %88.38
        ["30000000101003"] = new OfficialIdentityResult { NationalId = "30000000101003", FullName = "طالب تجريبي 046", SeatNumber = "101003" },  // %88.01
        ["30000000101158"] = new OfficialIdentityResult { NationalId = "30000000101158", FullName = "طالب تجريبي 047", SeatNumber = "101158" },  // %87.65
        ["30000000100419"] = new OfficialIdentityResult { NationalId = "30000000100419", FullName = "طالب تجريبي 048", SeatNumber = "100419" },  // %87.24
        ["30000000100099"] = new OfficialIdentityResult { NationalId = "30000000100099", FullName = "طالب تجريبي 049", SeatNumber = "100099" },  // %86.84
        ["30000000100042"] = new OfficialIdentityResult { NationalId = "30000000100042", FullName = "طالب تجريبي 050", SeatNumber = "100042" },  // %86.45
        ["30000000101661"] = new OfficialIdentityResult { NationalId = "30000000101661", FullName = "طالب تجريبي 051", SeatNumber = "101661" },  // %86.10
        ["30000000100780"] = new OfficialIdentityResult { NationalId = "30000000100780", FullName = "طالب تجريبي 052", SeatNumber = "100780" },  // %85.66
        ["30000000100017"] = new OfficialIdentityResult { NationalId = "30000000100017", FullName = "طالب تجريبي 053", SeatNumber = "100017" },  // %85.17
        ["30000000101472"] = new OfficialIdentityResult { NationalId = "30000000101472", FullName = "طالب تجريبي 054", SeatNumber = "101472" },  // %84.82
        ["30000000100877"] = new OfficialIdentityResult { NationalId = "30000000100877", FullName = "طالب تجريبي 055", SeatNumber = "100877" },  // %84.34
        ["30000000100364"] = new OfficialIdentityResult { NationalId = "30000000100364", FullName = "طالب تجريبي 056", SeatNumber = "100364" },  // %84.02
        ["30000000100895"] = new OfficialIdentityResult { NationalId = "30000000100895", FullName = "طالب تجريبي 057", SeatNumber = "100895" },  // %83.69
        ["30000000101314"] = new OfficialIdentityResult { NationalId = "30000000101314", FullName = "طالب تجريبي 058", SeatNumber = "101314" },  // %83.33
        ["30000000100622"] = new OfficialIdentityResult { NationalId = "30000000100622", FullName = "طالب تجريبي 059", SeatNumber = "100622" },  // %82.84
        ["30000000100313"] = new OfficialIdentityResult { NationalId = "30000000100313", FullName = "طالب تجريبي 060", SeatNumber = "100313" },  // %82.41
        ["30000000100865"] = new OfficialIdentityResult { NationalId = "30000000100865", FullName = "طالب تجريبي 061", SeatNumber = "100865" },  // %81.93
        ["30000000100551"] = new OfficialIdentityResult { NationalId = "30000000100551", FullName = "طالب تجريبي 062", SeatNumber = "100551" },  // %81.42
        ["30000000100606"] = new OfficialIdentityResult { NationalId = "30000000100606", FullName = "طالب تجريبي 063", SeatNumber = "100606" },  // %80.95
        ["30000000101130"] = new OfficialIdentityResult { NationalId = "30000000101130", FullName = "طالب تجريبي 064", SeatNumber = "101130" },  // %80.52
        ["30000000100793"] = new OfficialIdentityResult { NationalId = "30000000100793", FullName = "طالب تجريبي 065", SeatNumber = "100793" },  // %79.89
        ["30000000100499"] = new OfficialIdentityResult { NationalId = "30000000100499", FullName = "طالب تجريبي 066", SeatNumber = "100499" },  // %79.29
        ["30000000101254"] = new OfficialIdentityResult { NationalId = "30000000101254", FullName = "طالب تجريبي 067", SeatNumber = "101254" },  // %78.82
        ["30000000101361"] = new OfficialIdentityResult { NationalId = "30000000101361", FullName = "طالب تجريبي 068", SeatNumber = "101361" },  // %78.21
        ["30000000101267"] = new OfficialIdentityResult { NationalId = "30000000101267", FullName = "طالب تجريبي 069", SeatNumber = "101267" },  // %77.55
        ["30000000100224"] = new OfficialIdentityResult { NationalId = "30000000100224", FullName = "طالب تجريبي 070", SeatNumber = "100224" },  // %76.83
        ["30000000101376"] = new OfficialIdentityResult { NationalId = "30000000101376", FullName = "طالب تجريبي 071", SeatNumber = "101376" },  // %75.96
        ["30000000101034"] = new OfficialIdentityResult { NationalId = "30000000101034", FullName = "طالب تجريبي 072", SeatNumber = "101034" },  // %75.06
        ["30000000101473"] = new OfficialIdentityResult { NationalId = "30000000101473", FullName = "طالب تجريبي 073", SeatNumber = "101473" },  // %73.80
        ["30000000101033"] = new OfficialIdentityResult { NationalId = "30000000101033", FullName = "طالب تجريبي 074", SeatNumber = "101033" },  // %72.19
        ["30000000101165"] = new OfficialIdentityResult { NationalId = "30000000101165", FullName = "طالب تجريبي 075", SeatNumber = "101165" },  // %70.46
        ["30000000101459"] = new OfficialIdentityResult { NationalId = "30000000101459", FullName = "طالب تجريبي 076", SeatNumber = "101459" },  // %64.63
    };

    public Task<OfficialIdentityResult?> GetByNationalIdAsync(string nationalId)
    {
        var key = nationalId?.Trim() ?? string.Empty;
        Data.TryGetValue(key, out var result);
        return Task.FromResult(result);
    }
}
