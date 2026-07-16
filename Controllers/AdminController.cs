using System.Globalization;
using System.Text.Json;
using ANU_Admissions.Data;
using ANU_Admissions.Models;
using ANU_Admissions.Resources;
using ANU_Admissions.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace ANU_Admissions.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly AppDbContext _context;
    private readonly IStringLocalizer<SharedResource> _l;
    private readonly Services.IAdmissionsGate _admissionsGate;
    private readonly Services.IAllocationService _allocationService;
    private readonly Services.IOfficialRecordsImportService _officialRecordsImportService;
    private readonly Services.IOfficialRecordsMaintenanceService _officialRecordsMaintenanceService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(AppDbContext context, IStringLocalizer<SharedResource> localizer,
        Services.IAdmissionsGate admissionsGate, Services.IAllocationService allocationService,
        Services.IOfficialRecordsImportService officialRecordsImportService,
        Services.IOfficialRecordsMaintenanceService officialRecordsMaintenanceService,
        ILogger<AdminController> logger)
    {
        _context = context;
        _l = localizer;
        _admissionsGate = admissionsGate;
        _allocationService = allocationService;
        _officialRecordsImportService = officialRecordsImportService;
        _officialRecordsMaintenanceService = officialRecordsMaintenanceService;
        _logger = logger;
    }

    public async Task<IActionResult> Dashboard()
    {
        var viewModel = new AdminDashboardViewModel
        {
            // Statistics
            TotalStudents = await _context.StudentProfiles.CountAsync(),

            CompletedProfiles = await _context.StudentProfiles
                .Where(sp => sp.EquivalentPercentage > 0)
                .CountAsync(),

            StudentsWithPreferences = await _context.Preferences
                .Select(p => p.StudentProfileId)
                .Distinct()
                .CountAsync(),

            AllocatedCount = await _context.Allocations.CountAsync(),

            PendingAllocation = await _context.StudentProfiles
                .Where(sp => sp.EquivalentPercentage > 0 &&
                            !_context.Allocations.Any(a => a.StudentProfileId == sp.Id))
                .CountAsync(),

            DocumentsUploadedCount = await _context.Documents
                .Select(d => d.StudentProfileId)
                .Distinct()
                .CountAsync(),

            TotalColleges = await _context.Colleges.CountAsync(),

            // Latest Profiles (last 5)
            LatestProfiles = await _context.StudentProfiles
                .Include(sp => sp.User)
                .OrderByDescending(sp => sp.ApplicationDate)
                .Take(5)
                .Select(sp => new RecentProfileDto
                {
                    StudentName = sp.User.FullName ?? "غير محدد",
                    Email = sp.User.Email ?? "غير متوفر",
                    EquivalentPercentage = sp.EquivalentPercentage,
                    Section = sp.Section,
                    ApplicationDate = sp.ApplicationDate
                })
                .ToListAsync(),

            // Latest Allocations (last 5)
            LatestAllocations = await _context.Allocations
                .Include(a => a.StudentProfile)
                    .ThenInclude(sp => sp.User)
                .Include(a => a.College)
                .OrderByDescending(a => a.AllocationDate)
                .Take(5)
                .Select(a => new RecentAllocationDto
                {
                    StudentName = a.StudentProfile.User.FullName ?? "غير محدد",
                    CollegeName = a.College.NameAr,
                    CollegeNameEn = a.College.NameEn,
                    StudentScore = a.StudentProfile.EquivalentPercentage,
                    AllocationDate = a.AllocationDate
                })
                .ToListAsync(),

            // Latest Documents (last 5)
            LatestDocuments = await _context.Documents
                .Include(d => d.StudentProfile)
                    .ThenInclude(sp => sp.User)
                .OrderByDescending(d => d.UploadedAt)
                .Take(5)
                .Select(d => new RecentDocumentDto
                {
                    StudentName = d.StudentProfile.User.FullName ?? "غير محدد",
                    DocumentType = d.DocumentType,
                    UploadedAt = d.UploadedAt
                })
                .ToListAsync()
        };

        return View(viewModel);
    }

    public async Task<IActionResult> RunAllocation()
    {
        ViewBag.GateStatus = await _admissionsGate.GetStatusAsync();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExecuteAllocation()
    {
        // Allocation must run against a stable set of applications and
        // preferences. Students can still edit while admissions are open, so
        // reject the operation until the admin closes admissions first.
        var gate = await _admissionsGate.GetStatusAsync();
        if (gate.IsOpen)
        {
            TempData["AllocationError"] = _l["CloseAdmissionsBeforeAllocation"].Value;
            return RedirectToAction(nameof(RunAllocation));
        }

        try
        {
            var result = await _allocationService.RunAsync(HttpContext.RequestAborted);
            if (result.TotalProcessed == 0)
            {
                TempData["AllocationError"] = "لا يوجد طلاب مؤهلين للتنسيق";
                return RedirectToAction(nameof(RunAllocation));
            }

            var summary = new
            {
                result.TotalProcessed,
                result.Accepted,
                result.Rejected,
                ExecutionTime = $"{result.Duration.TotalSeconds:F2} ثانية"
            };

            TempData["AllocationSuccess"] = true;
            TempData["AllocationSummary"] = JsonSerializer.Serialize(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute allocation");
            TempData["AllocationError"] = "حدث خطأ أثناء تنفيذ العملية. يرجى المحاولة مرة أخرى.";
        }

        return RedirectToAction(nameof(RunAllocation));
    }

    // ================================
    // APPLICANTS MANAGEMENT
    // ================================

    public async Task<IActionResult> Applicants(string? searchTerm, string? statusFilter)
    {
        var query = _context.StudentProfiles
            .Include(sp => sp.User)
            .AsQueryable();

        // Search filter
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(sp =>
                (sp.User.FullName != null && sp.User.FullName.Contains(searchTerm)) ||
                (sp.User.Email != null && sp.User.Email.Contains(searchTerm)) ||
                (sp.NationalId != null && sp.NationalId.Contains(searchTerm)));
        }

        // Status filter
        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            query = statusFilter switch
            {
                "allocated" => query.Where(sp => _context.Allocations.Any(a => a.StudentProfileId == sp.Id)),
                "pending" => query.Where(sp => sp.EquivalentPercentage > 0 &&
                                              !_context.Allocations.Any(a => a.StudentProfileId == sp.Id)),
                "incomplete" => query.Where(sp => sp.EquivalentPercentage == 0),
                _ => query
            };
        }

        var applicants = await query
            .OrderByDescending(sp => sp.ApplicationDate)
            .Select(sp => new ApplicantRowDto
            {
                ProfileId = sp.Id,
                StudentName = sp.User.FullName ?? "غير محدد",
                Email = sp.User.Email ?? "غير متوفر",
                NationalId = sp.NationalId,
                Section = sp.Section,
                EquivalentPercentage = sp.EquivalentPercentage,
                PreferencesCount = _context.Preferences.Count(p => p.StudentProfileId == sp.Id),
                HasAllocation = _context.Allocations.Any(a => a.StudentProfileId == sp.Id),
                DocumentsCount = _context.Documents.Count(d => d.StudentProfileId == sp.Id),
                ApplicationDate = sp.ApplicationDate
            })
            .ToListAsync();

        var viewModel = new ApplicantsListViewModel
        {
            SearchTerm = searchTerm,
            StatusFilter = statusFilter,
            Applicants = applicants
        };

        return View(viewModel);
    }

    public async Task<IActionResult> ApplicantDetails(int id)
    {
        var profile = await _context.StudentProfiles
            .Include(sp => sp.User)
            .FirstOrDefaultAsync(sp => sp.Id == id);

        if (profile == null)
        {
            TempData["AdminError"] = "الطالب غير موجود";
            return RedirectToAction(nameof(Applicants));
        }

        var viewModel = new ApplicantDetailsViewModel
        {
            // Profile Info
            ProfileId = profile.Id,
            StudentName = profile.User.FullName ?? "غير محدد",
            Email = profile.User.Email ?? "غير متوفر",
            PhoneNumber = profile.User.PhoneNumber ?? "غير متوفر",
            NationalId = profile.NationalId,
            SeatNumber = profile.SeatNumber,
            DateOfBirth = profile.DateOfBirth,
            Gender = profile.Gender,
            Address = profile.Address,
            City = profile.City,
            Governorate = profile.Governorate,

            // Academic Info
            HighSchoolName = profile.HighSchoolName,
            GraduationYear = profile.GraduationYear,
            TotalScore = profile.TotalScore,
            Percentage = profile.Percentage,
            EquivalentPercentage = profile.EquivalentPercentage,
            Section = profile.Section,
            CertificateType = profile.CertificateType ?? "غير محدد",
            ApplicationDate = profile.ApplicationDate,

            // Preferences
            Preferences = await _context.Preferences
                .Include(p => p.College)
                .Where(p => p.StudentProfileId == profile.Id)
                .OrderBy(p => p.Rank)
                .Select(p => new PreferenceDetailDto
                {
                    Rank = p.Rank,
                    CollegeName = p.College.NameAr,
                    MinimumScore = p.College.MinimumScore,
                    CreatedAt = p.CreatedAt
                })
                .ToListAsync(),

            // Allocation
            Allocation = await _context.Allocations
                .Include(a => a.College)
                .Where(a => a.StudentProfileId == profile.Id)
                .Select(a => new AllocationDetailDto
                {
                    CollegeName = a.College.NameAr,
                    AllocationDate = a.AllocationDate,
                    Status = a.Status,
                    DocumentsSubmitted = a.DocumentsSubmitted
                })
                .FirstOrDefaultAsync(),

            // Documents
            Documents = await _context.Documents
                .Where(d => d.StudentProfileId == profile.Id)
                .OrderByDescending(d => d.UploadedAt)
                .Select(d => new DocumentDetailDto
                {
                    Id = d.Id,
                    DocumentType = d.DocumentType,
                    FileName = d.FileName,
                    FileSize = d.FileSize,
                    UploadedAt = d.UploadedAt,
                    IsVerified = d.IsVerified
                })
                .ToListAsync()
        };

        return View(viewModel);
    }

    // ================================
    // COLLEGE RESULTS REPORT (read-only, admin-only, printable)
    // ================================

    [HttpGet]
    public async Task<IActionResult> CollegeResultsReport(int? collegeId)
    {
        var model = new CollegeResultsReportViewModel
        {
            SelectedCollegeId = collegeId,
            GeneratedAt = DateTime.Now,
            Colleges = await _context.Colleges
                .OrderBy(c => c.Id)
                .Select(c => new CollegeDropdownItem { Id = c.Id, NameAr = c.NameAr, NameEn = c.NameEn })
                .ToListAsync()
        };

        if (collegeId.HasValue)
        {
            var college = await _context.Colleges.FirstOrDefaultAsync(c => c.Id == collegeId.Value);
            if (college == null)
            {
                TempData["AdminError"] = _l["ErrCollegeNotFound"].Value;
                return RedirectToAction(nameof(CollegeResultsReport));
            }

            model.CollegeName = college.NameAr;
            model.CollegeNameEn = college.NameEn;
            model.Capacity = college.Capacity;

            // Accepted students, ordered server-side by official equivalent
            // percentage (highest first).
            var allocations = await _context.Allocations
                .Where(a => a.CollegeId == collegeId.Value)
                .Include(a => a.StudentProfile).ThenInclude(sp => sp.User)
                .Include(a => a.StudentProfile).ThenInclude(sp => sp.OfficialRecord)
                .OrderByDescending(a => a.StudentProfile.EquivalentPercentage)
                .ToListAsync();

            // Preference rank the student was accepted at (if still available).
            var profileIds = allocations.Select(a => a.StudentProfileId).ToList();
            var prefRanks = await _context.Preferences
                .Where(p => p.CollegeId == collegeId.Value && profileIds.Contains(p.StudentProfileId))
                .ToDictionaryAsync(p => p.StudentProfileId, p => p.Rank);

            int rank = 1;
            foreach (var a in allocations)
            {
                var sp = a.StudentProfile;
                var officialName = sp.OfficialRecord?.FullName;
                model.Students.Add(new CollegeResultStudentRowViewModel
                {
                    Rank = rank++,
                    FullName = !string.IsNullOrWhiteSpace(officialName)
                        ? officialName
                        : (sp.User?.FullName ?? "غير محدد"),
                    SeatNumber = sp.SeatNumber ?? sp.OfficialRecord?.SeatNumber ?? "-",
                    NationalIdMasked = MaskNationalId(sp.NationalId),
                    Section = sp.Section ?? "",
                    EquivalentPercentage = sp.EquivalentPercentage,
                    PreferenceRank = prefRanks.TryGetValue(sp.Id, out var r) ? r : (int?)null,
                    AllocationDate = a.AllocationDate
                });
            }

            model.AcceptedCount = model.Students.Count;
            if (model.Students.Count > 0)
            {
                model.HighestPercentage = model.Students.Max(s => s.EquivalentPercentage);
                model.LowestPercentage = model.Students.Min(s => s.EquivalentPercentage);
            }
        }

        return View(model);
    }

    // Masks the middle of a national id (admin report / printable PDF safety).
    private static string MaskNationalId(string? nationalId)
    {
        if (string.IsNullOrWhiteSpace(nationalId)) return "غير متوفر";
        var id = nationalId.Trim();
        if (id.Length <= 5) return id;
        return id[..3] + new string('•', id.Length - 5) + id[^2..];
    }

    // ================================
    // COLLEGE MANAGEMENT (admin only; existing College fields, no schema change)
    // ================================

    [HttpGet]
    public async Task<IActionResult> ManageColleges()
    {
        var colleges = await _context.Colleges
            .OrderBy(c => c.Id)
            .Select(c => new CollegeEditViewModel
            {
                Id = c.Id,
                NameAr = c.NameAr,
                NameEn = c.NameEn,
                Code = c.Code,
                Capacity = c.Capacity,
                MinimumScore = c.MinimumScore,
                AllowedSections = c.AllowedSections,
                IsActive = c.IsActive,
                PreferencesCount = _context.Preferences.Count(p => p.CollegeId == c.Id),
                AllocationsCount = _context.Allocations.Count(a => a.CollegeId == c.Id)
            })
            .ToListAsync();

        return View(colleges);
    }

    [HttpGet]
    public IActionResult CreateCollege()
    {
        return View("CollegeForm", new CollegeEditViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCollege(CollegeEditViewModel model)
    {
        if (!await ValidateCollegeAsync(model))
            return View("CollegeForm", model);

        var college = new College
        {
            NameAr = model.NameAr.Trim(),
            NameEn = (model.NameEn ?? "").Trim(),
            Code = model.Code.Trim(),
            Capacity = model.Capacity,
            MinimumScore = model.MinimumScore,
            AllowedSections = BuildAllowedSections(model.SelectedSections),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.Colleges.Add(college);
        await _context.SaveChangesAsync();

        await LogAuditAsync("Create College", $"كلية: {college.NameAr} ({college.Code})");
        TempData["AdminSuccess"] = _l["MsgCollegeAdded"].Value;
        return RedirectToAction(nameof(ManageColleges));
    }

    [HttpGet]
    public async Task<IActionResult> EditCollege(int id)
    {
        var c = await _context.Colleges.FirstOrDefaultAsync(x => x.Id == id);
        if (c == null)
        {
            TempData["AdminError"] = _l["ErrCollegeNotFound"].Value;
            return RedirectToAction(nameof(ManageColleges));
        }

        return View("CollegeForm", new CollegeEditViewModel
        {
            Id = c.Id,
            NameAr = c.NameAr,
            NameEn = c.NameEn,
            Code = c.Code,
            Capacity = c.Capacity,
            MinimumScore = c.MinimumScore,
            AllowedSections = c.AllowedSections,
            IsActive = c.IsActive,
            SelectedSections = (c.AllowedSections ?? "")
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .ToList()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCollege(CollegeEditViewModel model)
    {
        var c = await _context.Colleges.FirstOrDefaultAsync(x => x.Id == model.Id);
        if (c == null)
        {
            TempData["AdminError"] = _l["ErrCollegeNotFound"].Value;
            return RedirectToAction(nameof(ManageColleges));
        }

        if (!await ValidateCollegeAsync(model))
            return View("CollegeForm", model);

        c.NameAr = model.NameAr.Trim();
        c.NameEn = (model.NameEn ?? "").Trim();
        c.Code = model.Code.Trim();
        c.Capacity = model.Capacity;
        c.MinimumScore = model.MinimumScore;
        c.AllowedSections = BuildAllowedSections(model.SelectedSections);
        c.IsActive = model.IsActive;
        await _context.SaveChangesAsync();

        var visibilityLabel = c.IsActive ? "ظاهرة" : "مخفية";
        await LogAuditAsync("Edit College",
            $"كلية: {c.NameAr} ({c.Code}) | السعة: {c.Capacity} | الحد الأدنى: {c.MinimumScore}% | الحالة: {visibilityLabel}");
        TempData["AdminSuccess"] = _l["MsgCollegeUpdated"].Value;
        return RedirectToAction(nameof(ManageColleges));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleCollegeVisibility(int id)
    {
        var c = await _context.Colleges.FirstOrDefaultAsync(x => x.Id == id);
        if (c == null)
        {
            TempData["AdminError"] = _l["ErrCollegeNotFound"].Value;
            return RedirectToAction(nameof(ManageColleges));
        }

        c.IsActive = !c.IsActive;
        await _context.SaveChangesAsync();

        var label = c.IsActive ? _l["VisibleToStudents"].Value : _l["HiddenFromStudents"].Value;
        // Audit-log entry stays in Arabic for operational consistency
        var arLabel = c.IsActive ? "ظاهرة للطلاب" : "مخفية عن الطلاب";
        await LogAuditAsync("Toggle College Visibility",
            $"كلية: {c.NameAr} ({c.Code}) → {arLabel}");
        var displayName = ANU_Admissions.Helpers.DisplayHelper.CollegeName(c.NameAr, c.NameEn);
        TempData["AdminSuccess"] = string.Format(
            CultureInfo.CurrentCulture,
            _l["MsgVisibilityChanged"].Value,
            displayName,
            label);
        return RedirectToAction(nameof(ManageColleges));
    }

    // Delete confirmation page — shows how many preferences/allocations will be
    // removed along with the college.
    [HttpGet]
    public async Task<IActionResult> DeleteCollege(int id)
    {
        var c = await _context.Colleges.FirstOrDefaultAsync(x => x.Id == id);
        if (c == null)
        {
            TempData["AdminError"] = _l["ErrCollegeNotFound"].Value;
            return RedirectToAction(nameof(ManageColleges));
        }

        var vm = new CollegeEditViewModel
        {
            Id = c.Id,
            NameAr = c.NameAr,
            NameEn = c.NameEn,
            Code = c.Code,
            Capacity = c.Capacity,
            MinimumScore = c.MinimumScore,
            AllowedSections = c.AllowedSections,
            IsActive = c.IsActive,
            PreferencesCount = await _context.Preferences.CountAsync(p => p.CollegeId == c.Id),
            AllocationsCount = await _context.Allocations.CountAsync(a => a.CollegeId == c.Id)
        };
        return View(vm);
    }

    // Deletes a college and only its own preferences/allocations. Atomic.
    // Does NOT touch other colleges, students, users, or official records.
    // Does NOT re-run allocation — admin must trigger that manually.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCollegeConfirmed(int id)
    {
        var c = await _context.Colleges.FirstOrDefaultAsync(x => x.Id == id);
        if (c == null)
        {
            TempData["AdminError"] = _l["ErrCollegeNotFound"].Value;
            return RedirectToAction(nameof(ManageColleges));
        }

        try
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            // College → Preferences/Allocations FKs use DeleteBehavior.Restrict,
            // so we must delete the dependents explicitly before the college row.
            var prefsDeleted = await _context.Preferences
                .Where(p => p.CollegeId == id)
                .ExecuteDeleteAsync();

            var allocsDeleted = await _context.Allocations
                .Where(a => a.CollegeId == id)
                .ExecuteDeleteAsync();

            _context.Colleges.Remove(c);
            await _context.SaveChangesAsync();

            await LogAuditAsync("Delete College",
                $"كلية: {c.NameAr} ({c.Code}) | رغبات محذوفة: {prefsDeleted} | تنسيقات محذوفة: {allocsDeleted}");

            await transaction.CommitAsync();

            var displayName = ANU_Admissions.Helpers.DisplayHelper.CollegeName(c.NameAr, c.NameEn);
            TempData["AdminSuccess"] = string.Format(
                CultureInfo.CurrentCulture,
                _l["MsgCollegeDeleted"].Value,
                displayName,
                prefsDeleted,
                allocsDeleted);
        }
        catch (Exception)
        {
            TempData["AdminError"] = _l["ErrCollegeDeleteFailed"].Value;
        }

        return RedirectToAction(nameof(ManageColleges));
    }

    // Server-side validation. Returns true if valid; otherwise fills ModelState.
    private async Task<bool> ValidateCollegeAsync(CollegeEditViewModel model)
    {
        if (!ModelState.IsValid) return false;

        // Without at least one allowed section the college would be invisible to
        // every student (the section filter requires a match), so fail loudly.
        var canonicalSelected = (model.SelectedSections ?? new List<string>())
            .Select(s => s?.Trim() ?? "")
            .Where(s => CollegeEditViewModel.CanonicalSections.Contains(s))
            .Distinct()
            .ToList();
        if (canonicalSelected.Count == 0)
        {
            ModelState.AddModelError(nameof(model.SelectedSections),
                _l["MustChooseSection"].Value);
            return false;
        }

        var code = model.Code.Trim();
        var codeTaken = await _context.Colleges
            .AnyAsync(c => c.Code == code && c.Id != model.Id);
        if (codeTaken)
        {
            ModelState.AddModelError(nameof(model.Code), _l["ErrCodeTaken"].Value);
            return false;
        }

        return true;
    }

    // Combines the chosen sections into a comma-separated AllowedSections string,
    // keeping only canonical values (guards against tampered/invalid input).
    private static string BuildAllowedSections(List<string>? selected)
    {
        if (selected == null) return string.Empty;
        var valid = selected
            .Select(s => s.Trim())
            .Where(s => CollegeEditViewModel.CanonicalSections.Contains(s))
            .Distinct();
        return string.Join(",", valid);
    }

    // ================================
    // SYSTEM CONTROL
    // ================================

    public async Task<IActionResult> SystemControl()
    {
        var gate = await _admissionsGate.GetStatusAsync();
        ViewBag.AdmissionsOpen = gate.IsOpen;
        ViewBag.GateStatus = gate;

        // Get statistics for display
        ViewBag.Stats = new
        {
            AllocationsCount = await _context.Allocations.CountAsync(),
            PreferencesCount = await _context.Preferences.CountAsync(),
            DocumentsCount = await _context.Documents.CountAsync(),
            ProfilesCount = await _context.StudentProfiles.CountAsync()
        };

        // Get recent audit logs
        var recentLogs = await _context.AuditLogs
            .OrderByDescending(a => a.PerformedAt)
            .Take(10)
            .ToListAsync();

        ViewBag.RecentLogs = recentLogs;

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetAllocations()
    {
        try
        {
            var count = await _context.Allocations.CountAsync();

            // Delete all allocations
            _context.Allocations.RemoveRange(await _context.Allocations.ToListAsync());

            // Reset FinalCutoff for all colleges
            var colleges = await _context.Colleges.ToListAsync();
            foreach (var college in colleges)
            {
                college.FinalCutoff = null;
            }

            await _context.SaveChangesAsync();

            // Log action
            await LogAuditAsync("Reset Allocations", $"Deleted {count} allocations and reset college cutoffs");

            TempData["AdminSuccess"] = $"تم حذف {count} تنسيق بنجاح";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset allocations");
            TempData["AdminError"] = "حدث خطأ أثناء تنفيذ العملية. يرجى المحاولة مرة أخرى.";
        }

        return RedirectToAction(nameof(SystemControl));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPreferences()
    {
        try
        {
            var count = await _context.Preferences.CountAsync();
            _context.Preferences.RemoveRange(await _context.Preferences.ToListAsync());
            await _context.SaveChangesAsync();

            await LogAuditAsync("Reset Preferences", $"Deleted {count} preferences");

            TempData["AdminSuccess"] = $"تم حذف {count} رغبة بنجاح";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset preferences");
            TempData["AdminError"] = "حدث خطأ أثناء تنفيذ العملية. يرجى المحاولة مرة أخرى.";
        }

        return RedirectToAction(nameof(SystemControl));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetDocuments()
    {
        try
        {
            var count = await _context.Documents.CountAsync();

            // The current document workflow stores metadata only; there are no
            // physical uploads to leave behind on disk.
            _context.Documents.RemoveRange(await _context.Documents.ToListAsync());
            await _context.SaveChangesAsync();

            await LogAuditAsync("Reset Documents", $"Deleted {count} document records");

            TempData["AdminSuccess"] = $"تم حذف {count} مستند بنجاح";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset documents");
            TempData["AdminError"] = "حدث خطأ أثناء تنفيذ العملية. يرجى المحاولة مرة أخرى.";
        }

        return RedirectToAction(nameof(SystemControl));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetEverything()
    {
        try
        {
            var allocCount = await _context.Allocations.CountAsync();
            var prefCount = await _context.Preferences.CountAsync();
            var docCount = await _context.Documents.CountAsync();

            // Delete in order: docs -> allocs -> prefs
            _context.Documents.RemoveRange(await _context.Documents.ToListAsync());
            _context.Allocations.RemoveRange(await _context.Allocations.ToListAsync());
            _context.Preferences.RemoveRange(await _context.Preferences.ToListAsync());

            // Reset college cutoffs
            var colleges = await _context.Colleges.ToListAsync();
            foreach (var college in colleges)
            {
                college.FinalCutoff = null;
            }

            await _context.SaveChangesAsync();

            await LogAuditAsync("Reset Everything",
                $"Deleted {allocCount} allocations, {prefCount} preferences, {docCount} documents");

            TempData["AdminSuccess"] = "تم إعادة تعيين النظام بالكامل (الملفات الشخصية محفوظة)";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset everything");
            TempData["AdminError"] = "حدث خطأ أثناء تنفيذ العملية. يرجى المحاولة مرة أخرى.";
        }

        return RedirectToAction(nameof(SystemControl));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleAdmissions()
    {
        try
        {
            var setting = await _context.SystemSettings
                .FirstOrDefaultAsync(s => s.Key == "AdmissionsOpen");

            if (setting == null)
            {
                setting = new SystemSetting
                {
                    Key = "AdmissionsOpen",
                    Value = "false",
                    LastModified = DateTime.UtcNow,
                    ModifiedBy = User.Identity?.Name ?? "Admin"
                };
                _context.SystemSettings.Add(setting);
            }
            else
            {
                setting.Value = setting.Value == "true" ? "false" : "true";
                setting.LastModified = DateTime.UtcNow;
                setting.ModifiedBy = User.Identity?.Name ?? "Admin";
            }

            await _context.SaveChangesAsync();

            var status = setting.Value == "true" ? "فتح" : "إغلاق";
            await LogAuditAsync("Toggle Admissions", $"Changed admissions status to: {setting.Value}");

            TempData["AdminSuccess"] = $"تم {status} باب القبول";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle admissions");
            TempData["AdminError"] = "حدث خطأ أثناء تنفيذ العملية. يرجى المحاولة مرة أخرى.";
        }

        return RedirectToAction(nameof(SystemControl));
    }

    // Writes (or clears) the AdmissionsStartAt / AdmissionsEndAt SystemSettings
    // rows. No schema change — both values live as ISO 8601 strings keyed by
    // their setting name. Empty inputs clear the row, so the admin can revert
    // back to the manual master switch with no scheduled window.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetAdmissionsSchedule(DateTime? startAt, DateTime? endAt)
    {
        if (startAt.HasValue && endAt.HasValue && startAt.Value >= endAt.Value)
        {
            TempData["AdminError"] = _l["InvalidAdmissionsSchedule"].Value;
            return RedirectToAction(nameof(SystemControl));
        }

        await UpsertSettingAsync(Services.AdmissionsGateService.KeyStartAt,
            startAt?.ToString("o", System.Globalization.CultureInfo.InvariantCulture) ?? "");
        await UpsertSettingAsync(Services.AdmissionsGateService.KeyEndAt,
            endAt?.ToString("o", System.Globalization.CultureInfo.InvariantCulture) ?? "");

        await LogAuditAsync("Set Admissions Schedule",
            $"StartAt={startAt?.ToString("o") ?? "(cleared)"}, EndAt={endAt?.ToString("o") ?? "(cleared)"}");

        TempData["AdminSuccess"] = _l["AdmissionsScheduleSaved"].Value;
        return RedirectToAction(nameof(SystemControl));
    }

    // Inserts or updates a single SystemSettings row by Key.
    private async Task UpsertSettingAsync(string key, string value)
    {
        var row = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);
        if (row == null)
        {
            _context.SystemSettings.Add(new SystemSetting
            {
                Key = key,
                Value = value,
                LastModified = DateTime.UtcNow,
                ModifiedBy = User.Identity?.Name ?? "Admin"
            });
        }
        else
        {
            row.Value = value;
            row.LastModified = DateTime.UtcNow;
            row.ModifiedBy = User.Identity?.Name ?? "Admin";
        }
        await _context.SaveChangesAsync();
    }

    // ================================
    // OFFICIAL RECORDS IMPORT
    // ================================

    [HttpGet]
    public async Task<IActionResult> ImportOfficialRecords()
    {
        await PopulateImportStatsAsync();
        return View(new ImportOfficialRecordsViewModel());
    }

    // Confirmation page before deleting the imported official results.
    [HttpGet]
    public async Task<IActionResult> DeleteOfficialRecords()
    {
        ViewBag.TotalRecords = await _context.OfficialStudentRecords.CountAsync();
        ViewBag.LinkedProfiles = await _context.StudentProfiles.CountAsync(p => p.OfficialRecordId != null);
        ViewBag.AllocationsCount = await _context.Allocations.CountAsync();
        ViewBag.VerifiedPreferences = await _context.Preferences
            .CountAsync(pr => pr.StudentProfile.OfficialRecordId != null);
        return View();
    }

    // Deletes ALL imported official records (OfficialStudentRecords only).
    // Blocked while any student is verified against them. Transactional.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteOfficialRecordsConfirmed()
    {
        var outcome = await _officialRecordsMaintenanceService.DeleteAllAsync(
            GetOfficialRecordsMaintenanceActor(),
            HttpContext.RequestAborted);

        switch (outcome.Status)
        {
            case Services.OfficialRecordsDeleteStatus.Deleted:
                TempData["AdminSuccess"] =
                    _l["MsgOfficialRecordsDeleted", outcome.DeletedRecords].Value;
                return RedirectToAction(nameof(ImportOfficialRecords));

            case Services.OfficialRecordsDeleteStatus.NoRecords:
                TempData["AdminError"] = _l["NoOfficialToDelete"].Value;
                return RedirectToAction(nameof(ImportOfficialRecords));

            case Services.OfficialRecordsDeleteStatus.LinkedProfilesExist:
                TempData["AdminError"] = _l["DeleteBlockedMsg"].Value;
                return RedirectToAction(nameof(DeleteOfficialRecords));

            case Services.OfficialRecordsDeleteStatus.Busy:
                TempData["AdminError"] = _l["OfficialMaintenanceBusy"].Value;
                return RedirectToAction(nameof(DeleteOfficialRecords));

            default:
                TempData["AdminError"] = _l["ErrOfficialRecordsDelete"].Value;
                return RedirectToAction(nameof(DeleteOfficialRecords));
        }
    }

    // Confirmation page: reset verified applications so official results can be
    // deleted afterwards. Unlinks students from official records.
    [HttpGet]
    public async Task<IActionResult> ResetVerifiedApplications()
    {
        ViewBag.LinkedProfiles = await _context.StudentProfiles.CountAsync(p => p.OfficialRecordId != null);
        ViewBag.LinkedPreferences = await _context.Preferences
            .CountAsync(pr => pr.StudentProfile.OfficialRecordId != null);
        ViewBag.LinkedAllocations = await _context.Allocations
            .CountAsync(a => a.StudentProfile.OfficialRecordId != null);
        return View();
    }

    // Resets verified students back to a pre-verification state:
    //  - unlinks OfficialRecordId
    //  - clears the official-derived fields (NationalId, SeatNumber, scores)
    //  - removes their preferences/allocations (they must re-apply)
    // Does NOT touch user accounts, colleges, admin, or OfficialStudentRecords.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetVerifiedApplicationsConfirmed()
    {
        var outcome = await _officialRecordsMaintenanceService
            .ResetVerifiedApplicationsAsync(
                GetOfficialRecordsMaintenanceActor(),
                HttpContext.RequestAborted);

        switch (outcome.Status)
        {
            case Services.VerifiedApplicationsResetStatus.Reset:
                TempData["AdminSuccess"] = _l[
                    "MsgVerifiedApplicationsReset",
                    outcome.ResetProfiles,
                    outcome.DeletedPreferences,
                    outcome.DeletedAllocations].Value;
                break;

            case Services.VerifiedApplicationsResetStatus.NoLinkedProfiles:
                TempData["AdminSuccess"] = _l["NoLinkedStudents"].Value;
                break;

            case Services.VerifiedApplicationsResetStatus.Busy:
                TempData["AdminError"] = _l["OfficialMaintenanceBusy"].Value;
                break;

            default:
                TempData["AdminError"] = _l["ErrVerifiedApplicationsReset"].Value;
                break;
        }

        return RedirectToAction(nameof(DeleteOfficialRecords));
    }

    // Shared stats for the import page: total/eligible records, last batch and
    // a flag the view uses to block re-upload while data already exists.
    private async Task PopulateImportStatsAsync()
    {
        var total = await _context.OfficialStudentRecords.CountAsync();
        ViewBag.TotalRecords = total;
        ViewBag.HasRecords = total > 0;
        ViewBag.EligibleRecords = await _context.OfficialStudentRecords.CountAsync(o => o.IsEligible);

        var last = await _context.OfficialStudentRecords
            .OrderByDescending(o => o.ImportedAt)
            .Select(o => new { o.ImportedAt, o.ImportBatch })
            .FirstOrDefaultAsync();

        ViewBag.LastImportAt = last?.ImportedAt;
        ViewBag.LastImportBatch = last?.ImportBatch;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(Services.OfficialRecordsFileLimits.MaxRequestSizeBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = Services.OfficialRecordsFileLimits.MaxRequestSizeBytes)]
    public async Task<IActionResult> ImportOfficialRecords(ImportOfficialRecordsViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var outcome = await _officialRecordsImportService.ImportAsync(
            model.ExcelFile!,
            model.MaxScore,
            model.ImportBatch,
            model.AbortOnAnyOverflow,
            HttpContext.RequestAborted);

        if (outcome.Status == Services.OfficialRecordsImportStatus.RecordsAlreadyExist)
        {
            await PopulateImportStatsAsync();
            TempData["AdminError"] = _l["ImportFileExists"].Value;
            return View(model);
        }

        if (outcome.Status == Services.OfficialRecordsImportStatus.Busy)
        {
            await PopulateImportStatsAsync();
            TempData["AdminError"] = _l["OfficialMaintenanceBusy"].Value;
            return View(model);
        }

        var fileValidationErrorKey = outcome.Status switch
        {
            Services.OfficialRecordsImportStatus.InvalidFileType => "InvalidExcelFileType",
            Services.OfficialRecordsImportStatus.EmptyFile => "EmptyExcelFile",
            Services.OfficialRecordsImportStatus.FileTooLarge => "ExcelFileTooLarge",
            Services.OfficialRecordsImportStatus.InvalidFileContent => "InvalidExcelFileContent",
            Services.OfficialRecordsImportStatus.UnsafeArchive => "UnsafeExcelArchive",
            _ => null
        };
        if (fileValidationErrorKey != null)
        {
            ModelState.AddModelError(nameof(model.ExcelFile),
                _l[fileValidationErrorKey].Value);
            return View(model);
        }

        var result = outcome.Result!;
        if (!result.Aborted)
        {
            await LogAuditAsync("Import Official Records",
                $"Batch={result.ImportBatch}, MaxScore={model.MaxScore}, " +
                $"Read={result.TotalRowsRead}, Imported={result.Imported + result.NotEligibleImported} " +
                $"(eligible {result.Imported}, ineligible {result.NotEligibleImported})");
        }

        return ResultView(result);
    }

    private IActionResult ResultView(ImportOfficialRecordsResultViewModel result)
    {
        var total = _context.OfficialStudentRecords.Count();
        ViewBag.ImportResult = result;
        ViewBag.TotalRecords = total;
        ViewBag.HasRecords = total > 0;
        ViewBag.EligibleRecords = _context.OfficialStudentRecords.Count(o => o.IsEligible);
        return View("ImportOfficialRecords", new ImportOfficialRecordsViewModel
        {
            MaxScore = result.MaxScoreUsed
        });
    }

    // ================================
    // HELPER METHODS
    // ================================

    private Services.OfficialRecordsMaintenanceActor GetOfficialRecordsMaintenanceActor()
        => new(
            User.Identity?.Name ?? "Admin",
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown");

    private async Task LogAuditAsync(string action, string? details = null)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

        var log = new AuditLog
        {
            Action = action,
            PerformedBy = User.Identity?.Name ?? "Admin",
            PerformedAt = DateTime.UtcNow,
            Details = details,
            IpAddress = ipAddress
        };

        _context.AuditLogs.Add(log);
        await _context.SaveChangesAsync();
    }

    // Convenience wrapper around the central gate (manual switch + scheduled window).
    private async Task<bool> IsAdmissionsOpenAsync()
        => (await _admissionsGate.GetStatusAsync()).IsOpen;
}
