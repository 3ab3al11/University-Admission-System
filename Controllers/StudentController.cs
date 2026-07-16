using System.Globalization;
using ANU_Admissions.Data;
using ANU_Admissions.Models;
using ANU_Admissions.Resources;
using ANU_Admissions.Services;
using ANU_Admissions.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace ANU_Admissions.Controllers;

[Authorize(Roles = "Student")]
public class StudentController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _context;
    private readonly Services.IStudentVerificationService _studentVerificationService;
    private readonly Services.IAdmissionsGate _admissionsGate;
    private readonly IStringLocalizer<SharedResource> _l;

    public StudentController(
        UserManager<ApplicationUser> userManager,
        AppDbContext context,
        Services.IStudentVerificationService studentVerificationService,
        Services.IAdmissionsGate admissionsGate,
        IStringLocalizer<SharedResource> localizer)
    {
        _userManager = userManager;
        _context = context;
        _studentVerificationService = studentVerificationService;
        _admissionsGate = admissionsGate;
        _l = localizer;
    }

    public async Task<IActionResult> Dashboard()
    {
        // Load current user
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account");
        }

        // Load student profile (with official record for the official name)
        var profile = await _context.StudentProfiles
            .Include(p => p.OfficialRecord)
            .FirstOrDefaultAsync(p => p.UserId == user.Id);

        // Count related data
        int preferencesCount = 0;
        bool hasAllocation = false;

        if (profile != null)
        {
            preferencesCount = await _context.Preferences
                .Where(p => p.StudentProfileId == profile.Id)
                .CountAsync();

            hasAllocation = await _context.Allocations
                .AnyAsync(a => a.StudentProfileId == profile.Id);
        }

        // Build ViewModel
        var viewModel = new StudentDashboardViewModel
        {
            FullName = user.FullName ?? "غير محدد",
            OfficialFullName = profile?.OfficialRecord?.FullName,
            Email = user.Email ?? "غير متوفر",
            PhoneNumber = user.PhoneNumber ?? "غير متوفر",
            ParentPhoneNumber = user.ParentPhoneNumber ?? "غير متوفر",
            EquivalentPercentage = profile?.EquivalentPercentage ?? 0,
            Section = profile?.Section ?? "غير محدد",
            HasProfile = profile != null && profile.EquivalentPercentage > 0,
            HasPreferences = preferencesCount > 0,
            HasAllocation = hasAllocation,
            HasUploadedDocuments = false
        };

        // Determine status. Online document upload was removed, so an allocated
        // student is considered complete (attendance happens in person).
        if (!viewModel.HasProfile)
        {
            viewModel.Status = ApplicationStatus.NeedsProfile;
        }
        else if (!viewModel.HasPreferences)
        {
            viewModel.Status = ApplicationStatus.NeedsPreferences;
        }
        else if (!viewModel.HasAllocation)
        {
            viewModel.Status = ApplicationStatus.PendingAllocation;
        }
        else
        {
            viewModel.Status = ApplicationStatus.Completed;
        }

        // Full gate status drives both the simple boolean banner and the new
        // schedule-aware messaging (NotStarted / Expired etc.).
        var gate = await _admissionsGate.GetStatusAsync();
        ViewBag.AdmissionsOpen = gate.IsOpen;
        ViewBag.GateStatus = gate;

        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> ApplicationForm()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account");
        }

        var profile = await _context.StudentProfiles
            .Include(p => p.OfficialRecord)
            .FirstOrDefaultAsync(p => p.UserId == user.Id);

        var model = new ApplicationFormViewModel
        {
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            ParentPhoneNumber = user.ParentPhoneNumber,
            Address = profile?.Address
        };

        if (profile?.OfficialRecord != null)
        {
            PopulateFromOfficial(model, profile.OfficialRecord, profile.Section);
        }
        else if (profile != null)
        {
            // Not linked yet but has a section preference saved -> prefill it.
            model.Track = profile.Section;
        }

        var gate = await _admissionsGate.GetStatusAsync();
        ViewBag.AdmissionsOpen = gate.IsOpen;
        ViewBag.GateStatus = gate;
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplicationForm(ApplicationFormViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account");
        }
        model.Email = user.Email;

        // Block all writes while admissions are closed.
        if (!await IsAdmissionsOpenAsync())
        {
            TempData["StudentError"] = _l["St_AdmissionsClosedSave"].Value;
            return RedirectToAction(nameof(Dashboard));
        }

        var profile = await _context.StudentProfiles
            .Include(p => p.OfficialRecord)
            .FirstOrDefaultAsync(p => p.UserId == user.Id);

        // Already linked -> official data is read-only; nothing to re-verify.
        if (profile?.OfficialRecord != null)
        {
            PopulateFromOfficial(model, profile.OfficialRecord, profile.Section);
            TempData["StudentWarning"] = _l["St_AlreadyLinkedReadOnly"].Value;
            return View(model);
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var outcome = await _studentVerificationService.VerifyAndSaveAsync(
            user,
            profile,
            new Services.StudentVerificationRequest(
                model.NationalId!,
                model.SeatNumber!,
                model.Track!,
                model.PhoneNumber,
                model.ParentPhoneNumber,
                model.Address),
            HttpContext.RequestAborted);

        if (outcome.Status == Services.StudentVerificationStatus.AlreadyLinked)
        {
            profile = outcome.Profile!;
            PopulateFromOfficial(model, profile.OfficialRecord!, profile.Section);
            TempData["StudentWarning"] = _l["St_AlreadyLinkedReadOnly"].Value;
            return View(model);
        }

        switch (outcome.Status)
        {
            case Services.StudentVerificationStatus.InvalidSection:
                ModelState.AddModelError(nameof(model.Track), _l["St_TrackInvalid"].Value);
                break;
            case Services.StudentVerificationStatus.StudentPhoneTaken:
                ModelState.AddModelError(nameof(model.PhoneNumber), _l["Acc_StudentPhoneTaken"].Value);
                break;
            case Services.StudentVerificationStatus.ParentPhoneLimitReached:
                ModelState.AddModelError(nameof(model.ParentPhoneNumber), _l["Acc_ParentPhoneMaxed"].Value);
                break;
            case Services.StudentVerificationStatus.IdentityLookupFailed:
                ModelState.AddModelError(string.Empty, _l["St_IdentityLookupError"].Value);
                break;
            case Services.StudentVerificationStatus.NationalIdNotFound:
                ModelState.AddModelError(nameof(model.NationalId), _l["St_NationalIdNotFound"].Value);
                break;
            case Services.StudentVerificationStatus.SeatNumberMismatch:
                ModelState.AddModelError(nameof(model.SeatNumber), _l["St_SeatMismatch"].Value);
                break;
            case Services.StudentVerificationStatus.SeatNotInOfficialRecords:
                ModelState.AddModelError(nameof(model.SeatNumber), _l["St_SeatNotInOfficial"].Value);
                break;
            case Services.StudentVerificationStatus.StudentNotEligible:
                ModelState.AddModelError(nameof(model.SeatNumber), _l["St_NotEligible"].Value);
                break;
            case Services.StudentVerificationStatus.SeatAlreadyLinked:
                ModelState.AddModelError(nameof(model.SeatNumber), _l["St_SeatLinkedElsewhere"].Value);
                break;
            case Services.StudentVerificationStatus.PhoneConflict:
                ModelState.AddModelError(nameof(model.PhoneNumber), _l["St_PhoneInUse"].Value);
                break;
            case Services.StudentVerificationStatus.SaveFailed:
                ModelState.AddModelError(string.Empty, _l["St_SaveError"].Value);
                break;
        }

        if (outcome.Status != Services.StudentVerificationStatus.Succeeded)
        {
            return View(model);
        }

        profile = outcome.Profile!;
        await LogStudentAuditAsync("Student Verification", profile,
            $"تم التحقق من الهوية وربط النتيجة الرسمية — الشعبة: {profile.Section}، " +
            $"النسبة المكافئة: {profile.EquivalentPercentage}%");

        TempData["StudentSuccess"] = _l["St_VerifiedOk"].Value;
        return RedirectToAction("Preferences");
    }

    private static void PopulateFromOfficial(ApplicationFormViewModel model,
        OfficialStudentRecord record, string? section)
    {
        model.IsLinked = true;
        model.SeatNumber = record.SeatNumber;
        model.FullName = record.FullName;
        model.TotalScore = record.TotalScore;
        model.MaxScore = record.MaxScore;
        model.Percentage = record.Percentage;
        model.EquivalentPercentage = record.EquivalentPercentage;
        model.StatusDescription = record.StatusDescription;
        model.Track = section;
    }

    [HttpGet]
    public async Task<IActionResult> Preferences()
    {
        // GUARD: Must have completed profile first
        var guardResult = await EnsureProfileCompletedAsync();
        if (guardResult != null) return guardResult;

        // Show an in-page "closed" message (with a back link) instead of redirecting,
        // so the student lands on a screen that explains what's going on.
        var gate = await _admissionsGate.GetStatusAsync();
        ViewBag.GateStatus = gate;
        if (!gate.IsOpen)
        {
            ViewBag.AdmissionsOpen = false;
            return View(new PreferencesViewModel());
        }
        ViewBag.AdmissionsOpen = true;

        var userId = _userManager.GetUserId(User);
        var profile = await _context.StudentProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId);

        // Guard: Must complete application first
        if (profile == null || profile.EquivalentPercentage == 0)
        {
            TempData["StudentWarning"] = _l["St_MustCompleteProfile"].Value;
            return RedirectToAction("ApplicationForm");
        }

        // GUARD (item 5): once allocated, preferences can no longer be edited.
        var alreadyAllocated = await _context.Allocations
            .AnyAsync(a => a.StudentProfileId == profile.Id);
        if (alreadyAllocated)
        {
            TempData["StudentWarning"] = _l["St_AlreadyAllocatedNoEdit"].Value;
            return RedirectToAction(nameof(AllocationStatus));
        }

        var model = new PreferencesViewModel
        {
            StudentTrack = profile.Section ?? "علمي رياضة",
            EquivalentPercentage = profile.EquivalentPercentage,
            MaxPreferences = GetMaxPreferencesForSection(profile.Section)
        };

        // Filter colleges by track from database
        model.AvailableColleges = await GetCollegesByTrackFromDb(model.StudentTrack);

        // Load existing preferences if any
        var existingPreferences = await _context.Preferences
            .Where(p => p.StudentProfileId == profile.Id)
            .OrderBy(p => p.Rank)
            .Select(p => p.CollegeId)
            .ToListAsync();

        if (existingPreferences.Count > 0)
        {
            model.SelectedCollegeIds = existingPreferences;
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Preferences(PreferencesViewModel vm)
    {
        // GUARD: Must have completed profile first
        var guardResult = await EnsureProfileCompletedAsync();
        if (guardResult != null) return guardResult;

        // GUARD: Check if admissions are open
        var admissionsOpen = await IsAdmissionsOpenAsync();
        if (!admissionsOpen)
        {
            TempData["StudentError"] = _l["St_AdmissionsClosedSave"].Value;
            return RedirectToAction(nameof(Dashboard));
        }

        var userId = _userManager.GetUserId(User);
        var profile = await _context.StudentProfiles
            .Include(p => p.OfficialRecord)
            .FirstOrDefaultAsync(p => p.UserId == userId);

        if (profile == null)
        {
            return RedirectToAction("ApplicationForm");
        }

        // GUARD (item 5): once allocated, preferences can no longer be edited,
        // even via a hand-crafted POST.
        var alreadyAllocated = await _context.Allocations
            .AnyAsync(a => a.StudentProfileId == profile.Id);
        if (alreadyAllocated)
        {
            TempData["StudentError"] = _l["St_AlreadyAllocatedNoEdit"].Value;
            return RedirectToAction(nameof(AllocationStatus));
        }

        var section = profile.Section;
        var maxPreferences = GetMaxPreferencesForSection(section);

        // Validate selected colleges
        if (vm.SelectedCollegeIds == null || vm.SelectedCollegeIds.Count == 0)
        {
            ModelState.AddModelError(nameof(vm.SelectedCollegeIds), _l["St_PrefAtLeastOne"].Value);
        }
        else if (vm.SelectedCollegeIds.Count > maxPreferences)
        {
            ModelState.AddModelError(nameof(vm.SelectedCollegeIds),
                string.Format(CultureInfo.CurrentCulture, _l["St_PrefMax"].Value, maxPreferences));
        }
        else if (vm.SelectedCollegeIds.Count != vm.SelectedCollegeIds.Distinct().Count())
        {
            ModelState.AddModelError(nameof(vm.SelectedCollegeIds),
                _l["St_PrefDuplicate"].Value);
        }

        // Validate every selected college is allowed for the student's section
        // (server-side enforcement — never trust the client).
        if (vm.SelectedCollegeIds != null && vm.SelectedCollegeIds.Count > 0)
        {
            var allowedIds = (await GetCollegesByTrackFromDb(section ?? ""))
                .Select(c => c.Id).ToHashSet();

            if (vm.SelectedCollegeIds.Any(id => !allowedIds.Contains(id)))
            {
                ModelState.AddModelError(nameof(vm.SelectedCollegeIds),
                    _l["St_PrefNotAvailableForTrack"].Value);
            }

            var hiddenNames = await _context.Colleges
                .Where(c => vm.SelectedCollegeIds.Contains(c.Id) && !c.IsActive)
                .Select(c => c.NameAr)
                .ToListAsync();

            foreach (var name in hiddenNames)
            {
                ModelState.AddModelError(nameof(vm.SelectedCollegeIds),
                    string.Format(CultureInfo.CurrentCulture, _l["St_PrefCollegeHidden"].Value, name));
            }

            var belowMin = await _context.Colleges
                .Where(c => vm.SelectedCollegeIds.Contains(c.Id)
                            && profile.EquivalentPercentage < c.MinimumScore)
                .Select(c => c.NameAr)
                .ToListAsync();

            foreach (var name in belowMin)
            {
                ModelState.AddModelError(nameof(vm.SelectedCollegeIds),
                    string.Format(CultureInfo.CurrentCulture, _l["St_PrefBelowMin"].Value, name));
            }
        }

        if (!ModelState.IsValid)
        {
            // Re-populate model with REAL data
            vm.StudentTrack = section ?? "";
            vm.EquivalentPercentage = profile.EquivalentPercentage;
            vm.MaxPreferences = maxPreferences;
            vm.AvailableColleges = await GetCollegesByTrackFromDb(vm.StudentTrack);
            return View(vm);
        }

        // Capture old preferences (for the audit log) before replacing them.
        var existingPreferences = await _context.Preferences
            .Include(p => p.College)
            .Where(p => p.StudentProfileId == profile.Id)
            .OrderBy(p => p.Rank)
            .ToListAsync();

        var oldNames = string.Join(" > ", existingPreferences.Select(p => p.College.NameAr));
        bool isUpdate = existingPreferences.Count > 0;

        if (existingPreferences.Count > 0)
        {
            _context.Preferences.RemoveRange(existingPreferences);
        }

        // Add new preferences in chosen rank order
        for (int i = 0; i < vm.SelectedCollegeIds!.Count; i++)
        {
            _context.Preferences.Add(new Preference
            {
                StudentProfileId = profile.Id,
                CollegeId = vm.SelectedCollegeIds[i],
                Rank = i + 1,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();

        // Audit log (item 11) — official name, seat, old/new preferences.
        var newColleges = await _context.Colleges
            .Where(c => vm.SelectedCollegeIds.Contains(c.Id))
            .ToListAsync();
        var newNames = string.Join(" > ", vm.SelectedCollegeIds
            .Select(id => newColleges.FirstOrDefault(c => c.Id == id)?.NameAr
                ?? id.ToString(CultureInfo.InvariantCulture)));

        await LogStudentAuditAsync(
            isUpdate ? "Update Preferences" : "Save Preferences",
            profile,
            $"الرغبات السابقة: [{(string.IsNullOrEmpty(oldNames) ? "لا يوجد" : oldNames)}] | " +
            $"الرغبات الجديدة: [{newNames}]");

        TempData["StudentSuccess"] = _l["St_PrefSavedOk"].Value;
        return RedirectToAction("AllocationStatus");
    }

    public async Task<IActionResult> AllocationStatus()
    {
        // GUARD: Must have saved preferences first
        var guardResult = await EnsurePreferencesExistAsync();
        if (guardResult != null) return guardResult;

        var userId = _userManager.GetUserId(User);
        var user = await _userManager.GetUserAsync(User);

        if (user == null)
        {
            return RedirectToAction("Login", "Account");
        }

        // Guard: Check ApplicationForm completed
        var profile = await _context.StudentProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId);

        if (profile == null || profile.EquivalentPercentage == 0)
        {
            TempData["StudentWarning"] = _l["St_MustCompleteProfile"].Value;
            return RedirectToAction("ApplicationForm");
        }

        // Guard: Check preferences submitted
        var hasPreferences = await _context.Preferences
            .AnyAsync(p => p.StudentProfile.UserId == userId);

        if (!hasPreferences)
        {
            TempData["StudentWarning"] = _l["St_MustChoosePrefs"].Value;
            return RedirectToAction("Preferences");
        }

        // Load preferences with college info
        var preferences = await _context.Preferences
            .Include(p => p.College)
            .Where(p => p.StudentProfileId == profile.Id)
            .OrderBy(p => p.Rank)
            .ToListAsync();

        // Check for allocation
        var allocation = await _context.Allocations
            .Include(a => a.College)
            .FirstOrDefaultAsync(a => a.StudentProfileId == profile.Id);

        var model = new AllocationStatusViewModel
        {
            FullName = user.FullName,
            Email = user.Email ?? "",
            EquivalentPercentage = profile.EquivalentPercentage,
            Track = profile.Section ?? "",
            Preferences = preferences.Select(p => new PreferenceDisplay
            {
                Rank = p.Rank,
                CollegeName = p.College.NameAr,
                MinimumScore = p.College.MinimumScore,
                FinalCutoff = p.College.FinalCutoff
            }).ToList(),
            Status = allocation != null ? "Allocated" : "Pending",
            Allocation = allocation != null ? new AllocationResultDisplay
            {
                CollegeName = allocation.College.NameAr,
                FinalCutoff = allocation.College.FinalCutoff ?? 0,
                AllocationDate = allocation.AllocationDate
            } : null
        };

        return View(model);
    }

    public async Task<IActionResult> AllocationResult()
    {
        // GUARD: Must have allocation result first
        var guardResult = await EnsureAllocationExistsAsync();
        if (guardResult != null) return guardResult;

        var model = await BuildNominationAsync();
        if (model == null)
        {
            TempData["StudentWarning"] = _l["St_NoAllocationYet"].Value;
            return RedirectToAction("AllocationStatus");
        }

        return View(model);
    }

    // Printable official nomination card (HTML + window.print()).
    [HttpGet]
    public async Task<IActionResult> NominationCard()
    {
        var guardResult = await EnsureAllocationExistsAsync();
        if (guardResult != null) return guardResult;

        var model = await BuildNominationAsync();
        if (model == null)
        {
            TempData["StudentWarning"] = _l["St_NoAllocationYet"].Value;
            return RedirectToAction("AllocationStatus");
        }

        return View(model);
    }

    // Legacy plain-text fallback download. The primary UI button points at the
    // printable NominationCard page; this stays as a simple text export.
    public async Task<IActionResult> DownloadNominationPdf()
    {
        // GUARD: Must have allocation result first
        var guardResult = await EnsureAllocationExistsAsync();
        if (guardResult != null) return guardResult;

        var n = await BuildNominationAsync();
        if (n == null)
        {
            TempData["StudentWarning"] = _l["St_NoAllocationYet"].Value;
            return RedirectToAction("AllocationStatus");
        }

        var content = $@"
==============================================================
        جامعة أسيوط الأهلية - Assiut National University
                     ورقة ترشيح للقبول
==============================================================

الاسم: {n.OfficialFullName}
الرقم القومي: {n.NationalId}
رقم الجلوس: {n.SeatNumber}
الشعبة: {n.Section}
النسبة المكافئة: {n.EquivalentPercentage}%

--------------------------------------------------------------

تم ترشيحك للقبول في:
{n.CollegeName}
الحد الأدنى النهائي للكلية: {n.FinalCutoff}%

--------------------------------------------------------------

تاريخ الترشيح: {n.AllocationDate:yyyy-MM-dd}
تاريخ الإصدار: {DateTime.Now:yyyy-MM-dd}

ملاحظة: يرجى الحضور لمقر الجامعة بالمستندات الورقية الأصلية لاستكمال التسجيل.
";

        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        return File(bytes, "text/plain; charset=utf-8", "nomination_letter.txt");
    }

    // Builds the official nomination view model for the current user from the
    // allocation + linked official record. Returns null if not allocated.
    // All values are server-sourced; nothing comes from the client.
    private async Task<NominationResultViewModel?> BuildNominationAsync()
    {
        var userId = _userManager.GetUserId(User);

        var allocation = await _context.Allocations
            .Include(a => a.StudentProfile).ThenInclude(sp => sp.User)
            .Include(a => a.StudentProfile).ThenInclude(sp => sp.OfficialRecord)
            .Include(a => a.College)
            .FirstOrDefaultAsync(a => a.StudentProfile.UserId == userId);

        if (allocation == null) return null;

        var sp = allocation.StudentProfile;

        // Official name comes from the linked official record; fall back to the
        // account name only if (unexpectedly) not linked.
        var officialName = sp.OfficialRecord?.FullName;
        if (string.IsNullOrWhiteSpace(officialName))
            officialName = sp.User.FullName;

        return new NominationResultViewModel
        {
            IsAllocated = true,
            OfficialFullName = string.IsNullOrWhiteSpace(officialName) ? "غير متوفر" : officialName,
            NationalId = sp.NationalId ?? "غير متوفر",
            SeatNumber = sp.OfficialRecord?.SeatNumber ?? sp.SeatNumber ?? "غير متوفر",
            Section = sp.Section ?? "",
            EquivalentPercentage = sp.EquivalentPercentage,
            CollegeName = allocation.College.NameAr,
            FinalCutoff = allocation.College.FinalCutoff ?? 0,
            AllocationDate = allocation.AllocationDate
        };
    }

    // Online document upload is no longer part of the flow. Documents are
    // delivered physically at the university (see the attendance instructions
    // on the result/nomination pages). These stubs keep old links safe:
    // GET redirects to the result page; POST never accepts any upload.
    [HttpGet]
    public IActionResult UploadDocuments()
    {
        return RedirectToAction(nameof(AllocationResult));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult UploadDocuments(UploadDocumentsViewModel model)
    {
        // Online document upload is disabled. Never accept or store any file.
        TempData["StudentWarning"] = _l["St_DocsUploadDisabled"].Value;
        return RedirectToAction(nameof(AllocationResult));
    }

    public async Task<IActionResult> FinalConfirmation()
    {
        // GUARD: Must have uploaded documents first
        var guardResult = await EnsureDocumentsUploadedAsync();
        if (guardResult != null) return guardResult;

        return View();
    }

    // ================================
    // SECTION (TRACK) PREFERENCE RULES
    // A college is available to a student's section when its AllowedSections
    // (managed by the admin) contains that section — EXCEPT FIN, which stays
    // hidden from all students by business rule. Max preferences per section
    // stays fixed (3 for science tracks, 2 for literary).
    // ================================
    private static int GetMaxPreferencesForSection(string? section) => section switch
    {
        "أدبي" => 2,
        _ => 3
    };

    // Data-driven: reads College.AllowedSections instead of hardcoded codes.
    private static bool IsCollegeAllowedForSection(string code, string? allowedSections, string? section)
    {
        if (code == "FIN") return false;                       // FIN hidden from students
        if (string.IsNullOrWhiteSpace(section) || string.IsNullOrWhiteSpace(allowedSections))
            return false;

        return allowedSections
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Contains(section);
    }

    /// <summary>
    /// Gets colleges available for a specific track, applying the section rules
    /// from each college's AllowedSections (FIN always excluded).
    /// </summary>
    private async Task<List<CollegeOption>> GetCollegesByTrackFromDb(string track)
    {
        var all = await _context.Colleges
            .Where(c => c.IsActive)
            .Select(c => new { c.Id, c.Code, c.NameAr, c.NameEn, c.MinimumScore, c.FinalCutoff, c.AllowedSections })
            .ToListAsync();

        return all
            .Where(c => IsCollegeAllowedForSection(c.Code, c.AllowedSections, track))
            .Select(c => new CollegeOption
            {
                Id = c.Id,
                Code = c.Code,
                NameAr = c.NameAr,
                NameEn = c.NameEn,
                MinimumScore = c.MinimumScore,
                FinalCutoff = c.FinalCutoff
            })
            .ToList();
    }


    // NOTE: The old client-side CalculateEquivalentPercentage endpoint was
    // removed. Scores/percentages are sourced ONLY from OfficialStudentRecord
    // during verification; the client can neither send nor compute them.

    // ================================
    // FLOW GATING HELPER METHODS
    // ================================

    /// <summary>
    /// Gets the current student profile for the logged-in user
    /// </summary>
    private async Task<StudentProfile?> GetCurrentProfileAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return null;

        return await _context.StudentProfiles
            .FirstOrDefaultAsync(p => p.UserId == user.Id);
    }

    /// <summary>
    /// Ensures profile exists and is completed (EquivalentPercentage > 0)
    /// Redirects to ApplicationForm if not
    /// </summary>
    private async Task<IActionResult?> EnsureProfileCompletedAsync()
    {
        var profile = await GetCurrentProfileAsync();

        if (profile == null || profile.EquivalentPercentage <= 0)
        {
            TempData["StudentError"] = _l["St_MustCompleteApplicationShort"].Value;
            return RedirectToAction(nameof(ApplicationForm));
        }

        return null; // No redirect needed
    }

    /// <summary>
    /// Ensures student has saved preferences
    /// Redirects to Preferences if not
    /// </summary>
    private async Task<IActionResult?> EnsurePreferencesExistAsync()
    {
        var profileCheck = await EnsureProfileCompletedAsync();
        if (profileCheck != null) return profileCheck;

        var profile = await GetCurrentProfileAsync();
        var hasPreferences = await _context.Preferences
            .AnyAsync(p => p.StudentProfileId == profile!.Id);

        if (!hasPreferences)
        {
            TempData["StudentError"] = _l["St_MustChoosePrefsShort"].Value;
            return RedirectToAction(nameof(Preferences));
        }

        return null;
    }

    /// <summary>
    /// Ensures student has an allocation
    /// Redirects to AllocationStatus if not
    /// </summary>
    private async Task<IActionResult?> EnsureAllocationExistsAsync()
    {
        var preferencesCheck = await EnsurePreferencesExistAsync();
        if (preferencesCheck != null) return preferencesCheck;

        var profile = await GetCurrentProfileAsync();
        var hasAllocation = await _context.Allocations
            .AnyAsync(a => a.StudentProfileId == profile!.Id);

        if (!hasAllocation)
        {
            TempData["StudentWarning"] = _l["St_AllocationNotAnnounced"].Value;
            return RedirectToAction(nameof(AllocationStatus));
        }

        return null;
    }

    /// <summary>
    /// Ensures student has uploaded documents
    /// Redirects to UploadDocuments if not
    /// </summary>
    private async Task<IActionResult?> EnsureDocumentsUploadedAsync()
    {
        var allocationCheck = await EnsureAllocationExistsAsync();
        if (allocationCheck != null) return allocationCheck;

        var profile = await GetCurrentProfileAsync();
        var hasDocuments = await _context.Documents
            .AnyAsync(d => d.StudentProfileId == profile!.Id);

        if (!hasDocuments)
        {
            TempData["StudentWarning"] = _l["St_MustUploadDocs"].Value;
            return RedirectToAction(nameof(UploadDocuments));
        }

        return null;
    }

    /// <summary>
    /// Convenience wrapper around the central gate. Existing call sites just
    /// need a bool; views that want the richer state read ViewBag.GateStatus.
    /// </summary>
    private async Task<bool> IsAdmissionsOpenAsync()
        => (await _admissionsGate.GetStatusAsync()).IsOpen;

    /// <summary>
    /// Writes a student-side audit entry using the existing AuditLog table
    /// (no schema change). Identifies the student by official name + seat.
    /// </summary>
    private async Task LogStudentAuditAsync(string action, StudentProfile profile, string? details = null)
    {
        var officialName = profile.OfficialRecord?.FullName ?? "(غير محدد)";
        var seat = profile.SeatNumber ?? profile.OfficialRecord?.SeatNumber ?? "-";
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

        _context.AuditLogs.Add(new AuditLog
        {
            Action = action,
            PerformedBy = $"{officialName} (جلوس {seat})",
            PerformedAt = DateTime.UtcNow,
            Details = details,
            IpAddress = ip
        });

        await _context.SaveChangesAsync();
    }
}
