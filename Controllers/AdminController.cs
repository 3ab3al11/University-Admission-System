using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using ANU_Admissions.Data;
using ANU_Admissions.Models;
using ANU_Admissions.Resources;
using ANU_Admissions.ViewModels;
using System.Data;
using System.Text.Json;
using ExcelDataReader;
using Microsoft.Data.SqlClient;

namespace ANU_Admissions.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly AppDbContext _context;
    private readonly IStringLocalizer<SharedResource> _l;
    private readonly Services.IAdmissionsGate _admissionsGate;
    private readonly ILogger<AdminController> _logger;

    public AdminController(AppDbContext context, IStringLocalizer<SharedResource> localizer,
        Services.IAdmissionsGate admissionsGate, ILogger<AdminController> logger)
    {
        _context = context;
        _l = localizer;
        _admissionsGate = admissionsGate;
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

    public IActionResult RunAllocation()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExecuteAllocation()
    {
        try
        {
            var startTime = DateTime.Now;

            // 1. Load real students from database with preferences
            // Students are ordered by equivalent percentage. If percentages are
            // equal, earlier application date gets priority.
            var students = await _context.StudentProfiles
                .Include(sp => sp.User)
                .Where(sp => sp.EquivalentPercentage > 0)
                .OrderByDescending(sp => sp.EquivalentPercentage)
                .ThenBy(sp => sp.ApplicationDate)
                .ToListAsync();

            if (!students.Any())
            {
                TempData["AllocationError"] = "لا يوجد طلاب مؤهلين للتنسيق";
                return RedirectToAction("RunAllocation");
            }

            // Load preferences separately (EF Core limitation with Include + OrderBy)
            var studentIds = students.Select(s => s.Id).ToList();
            var allPreferences = await _context.Preferences
                .Include(p => p.College)
                .Where(p => studentIds.Contains(p.StudentProfileId))
                .OrderBy(p => p.StudentProfileId)
                .ThenBy(p => p.Rank)
                .ToListAsync();

            // Group preferences by student
            var preferencesByStudent = allPreferences
                .GroupBy(p => p.StudentProfileId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // 2. Load colleges from database
            var colleges = await _context.Colleges.ToListAsync();
            var collegeCapacity = colleges.ToDictionary(c => c.Id, c => c.Capacity);
            var collegeAssignments = colleges.ToDictionary(c => c.Id, c => new List<(int StudentProfileId, decimal Score)>());

            // Reset FinalCutoff for ALL colleges so a stale cutoff from a previous run
            // cannot survive. Colleges that accept students get recomputed below;
            // colleges with no students in this run stay null.
            foreach (var college in colleges)
            {
                college.FinalCutoff = null;
            }

            // Run the whole rewrite (clear old -> insert new -> update cutoffs) as one
            // atomic unit. On any error the transaction is rolled back (it is disposed
            // without commit), so the previous allocation is preserved.
            using var transaction = await _context.Database.BeginTransactionAsync();

            // 3. Clear previous allocations (allow rerun)
            var existingAllocations = await _context.Allocations.ToListAsync();
            if (existingAllocations.Any())
            {
                _context.Allocations.RemoveRange(existingAllocations);
                await _context.SaveChangesAsync();
            }

            // 4. Run allocation algorithm
            int allocatedCount = 0;
            int rejectedCount = 0;

            foreach (var student in students)
            {
                bool allocated = false;

                // Get student preferences
                if (!preferencesByStudent.TryGetValue(student.Id, out var preferences) || !preferences.Any())
                {
                    rejectedCount++;
                    continue;
                }

                // Try each preference in rank order
                foreach (var pref in preferences.OrderBy(p => p.Rank))
                {
                    var college = pref.College;
                    var currentCount = collegeAssignments[college.Id].Count;

                    // Accept only if: capacity available, the student meets the
                    // minimum score, AND the college is currently allowed for the
                    // student's section (per AllowedSections, FIN excluded). The
                    // last check re-validates against any admin edits made AFTER
                    // the student saved preferences. Ordering/algorithm unchanged.
                    if (college.IsActive &&
                        currentCount < collegeCapacity[college.Id] &&
                        student.EquivalentPercentage >= college.MinimumScore &&
                        IsCollegeAllowedForSectionAllocation(college, student.Section))
                    {
                        // Allocate student to this college
                        collegeAssignments[college.Id].Add((student.Id, student.EquivalentPercentage));
                        
                        _context.Allocations.Add(new Allocation
                        {
                            StudentProfileId = student.Id,
                            CollegeId = college.Id,
                            AllocationDate = DateTime.UtcNow
                        });

                        allocated = true;
                        allocatedCount++;
                        break;
                    }
                }

                if (!allocated)
                {
                    rejectedCount++;
                }
            }

            // 5. Update FinalCutoff only for colleges that actually accepted students.
            // (All cutoffs were reset to null above, so empty colleges remain null.)
            foreach (var kvp in collegeAssignments)
            {
                var college = colleges.First(c => c.Id == kvp.Key);

                if (kvp.Value.Any())
                {
                    // FinalCutoff is the minimum score among accepted students
                    college.FinalCutoff = kvp.Value.Min(x => x.Score);
                    _context.Colleges.Update(college);
                }
            }

            // 6. Save all changes and commit the transaction atomically
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            var executionTime = (DateTime.Now - startTime).TotalSeconds;

            // 7. Prepare summary
            var summary = new
            {
                TotalProcessed = students.Count,
                Accepted = allocatedCount,
                Rejected = rejectedCount,
                ExecutionTime = $"{executionTime:F2} ثانية"
            };

            TempData["AllocationSuccess"] = true;
            TempData["AllocationSummary"] = System.Text.Json.JsonSerializer.Serialize(summary);
            
            return RedirectToAction("RunAllocation");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute allocation");
            TempData["AllocationError"] = "حدث خطأ أثناء تنفيذ العملية. يرجى المحاولة مرة أخرى.";
            return RedirectToAction("RunAllocation");
        }
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
            if (model.Students.Any())
            {
                model.HighestPercentage = model.Students.Max(s => s.EquivalentPercentage);
                model.LowestPercentage = model.Students.Min(s => s.EquivalentPercentage);
            }
        }

        return View(model);
    }

    // Allocation-time section guard: a college is acceptable for a student only
    // if its AllowedSections contains the student's section and it is not FIN.
    // Same data-driven rule used on the student preferences page.
    private static bool IsCollegeAllowedForSectionAllocation(College college, string? section)
    {
        if (college.Code == "FIN") return false;
        if (string.IsNullOrWhiteSpace(section) || string.IsNullOrWhiteSpace(college.AllowedSections))
            return false;

        return college.AllowedSections
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Contains(section);
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
        TempData["AdminSuccess"] = string.Format(_l["MsgVisibilityChanged"].Value, displayName, label);
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
            TempData["AdminSuccess"] = string.Format(_l["MsgCollegeDeleted"].Value,
                displayName, prefsDeleted, allocsDeleted);
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
            
            // TODO: Optionally delete physical files from wwwroot/uploads
            // For now, just delete DB records
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
        // Guard: never delete official results while students are linked to them.
        var linked = await _context.StudentProfiles.CountAsync(p => p.OfficialRecordId != null);
        if (linked > 0)
        {
            TempData["AdminError"] =
                "لا يمكن حذف بيانات النتائج الرسمية لأن هناك طلابًا تم التحقق منهم باستخدام هذه البيانات. " +
                "يجب إعادة ضبط بيانات التقديم والتنسيق أولًا قبل حذف النتائج الرسمية.";
            return RedirectToAction(nameof(DeleteOfficialRecords));
        }

        try
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            // Efficient bulk delete (single SQL DELETE, no loading 734k rows).
            var deleted = await _context.OfficialStudentRecords.ExecuteDeleteAsync();

            await LogAuditAsync("Delete Official Records",
                $"تم حذف {deleted} سجل من النتائج الرسمية المستوردة");

            await transaction.CommitAsync();

            TempData["AdminSuccess"] = $"تم حذف {deleted} سجل من النتائج الرسمية. يمكنك الآن رفع ملف جديد.";
        }
        catch (Exception)
        {
            TempData["AdminError"] = "حدث خطأ أثناء حذف بيانات النتائج. لم يتم حذف أي شيء.";
        }

        return RedirectToAction(nameof(ImportOfficialRecords));
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
        try
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            var prefsDeleted = await _context.Preferences
                .Where(pr => pr.StudentProfile.OfficialRecordId != null)
                .ExecuteDeleteAsync();

            var allocsDeleted = await _context.Allocations
                .Where(a => a.StudentProfile.OfficialRecordId != null)
                .ExecuteDeleteAsync();

            // Unlink + clear ONLY the official-verification-derived fields.
            // Section/contact stay; EquivalentPercentage=0 forces re-verification.
            var resetCount = await _context.StudentProfiles
                .Where(p => p.OfficialRecordId != null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.OfficialRecordId, (int?)null)
                    .SetProperty(p => p.NationalId, (string?)null)
                    .SetProperty(p => p.SeatNumber, (string?)null)
                    .SetProperty(p => p.TotalScore, 0m)
                    .SetProperty(p => p.Percentage, 0m)
                    .SetProperty(p => p.EquivalentPercentage, 0m));

            await LogAuditAsync("Reset verified applications",
                $"تم فصل {resetCount} طالب عن النتائج الرسمية، وحذف {prefsDeleted} رغبة و {allocsDeleted} تنسيق");

            await transaction.CommitAsync();

            TempData["AdminSuccess"] =
                $"تم فصل {resetCount} طالب عن النتائج الرسمية وحذف {prefsDeleted} رغبة و {allocsDeleted} تنسيق. " +
                "يمكنك الآن حذف بيانات النتائج الرسمية.";
        }
        catch (Exception)
        {
            TempData["AdminError"] = "حدث خطأ أثناء إعادة الضبط. لم يتم تنفيذ أي تغيير.";
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
    [RequestSizeLimit(200_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 200_000_000)]
    public async Task<IActionResult> ImportOfficialRecords(ImportOfficialRecordsViewModel model)
    {
        // GUARD (item 9): a results file is already imported. Block new uploads
        // (server-side, not only UI) until the existing data is cleared.
        if (await _context.OfficialStudentRecords.AnyAsync())
        {
            await PopulateImportStatsAsync();
            TempData["AdminError"] = "يوجد ملف نتائج مستورد بالفعل. يجب حذف البيانات الحالية أولًا قبل رفع ملف جديد.";
            return View(model);
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var ext = Path.GetExtension(model.ExcelFile!.FileName).ToLowerInvariant();
        if (ext != ".xlsx" && ext != ".xls")
        {
            ModelState.AddModelError(nameof(model.ExcelFile), "الملف يجب أن يكون .xlsx أو .xls");
            return View(model);
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = new ImportOfficialRecordsResultViewModel
        {
            MaxScoreUsed = model.MaxScore,
            ImportBatch = string.IsNullOrWhiteSpace(model.ImportBatch)
                ? $"Import-{DateTime.UtcNow:yyyyMMdd-HHmmss}"
                : model.ImportBatch
        };

        // Load existing seat numbers once so we can skip duplicates without
        // round-tripping per row.
        var existingSeats = new HashSet<string>(
            await _context.OfficialStudentRecords.Select(o => o.SeatNumber).ToListAsync());
        var seenInFile = new HashSet<string>();

        var connectionString = _context.Database.GetConnectionString()
            ?? throw new InvalidOperationException("Connection string not configured.");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();

        try
        {
            using var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, transaction)
            {
                DestinationTableName = "OfficialStudentRecords",
                BatchSize = 5000,
                BulkCopyTimeout = 600
            };
            ConfigureBulkCopyMappings(bulkCopy);

            var batch = CreateOfficialRecordsTable();

            using var stream = model.ExcelFile.OpenReadStream();
            using var reader = ExcelReaderFactory.CreateReader(stream);

            // Use first sheet (the file has one: "Stage_New_Search").
            int rowNumber = 0;
            bool isHeaderRowConsumed = false;

            while (reader.Read())
            {
                rowNumber++;

                // First row is the Arabic header — skip it.
                if (!isHeaderRowConsumed)
                {
                    isHeaderRowConsumed = true;
                    continue;
                }

                result.TotalRowsRead++;

                // Column order in the file:
                // 0: رقم الجلوس, 1: الاسم, 2: الدرجة,
                // 3: student_case, 4: student_case_desc, 5: c_flage
                var seatRaw = reader.GetValue(0);
                var nameRaw = reader.GetValue(1);
                var scoreRaw = reader.GetValue(2);
                var statusRaw = reader.FieldCount > 4 ? reader.GetValue(4) : null;

                var seatNumber = seatRaw?.ToString()?.Trim();
                var fullName = nameRaw?.ToString()?.Trim();

                if (string.IsNullOrEmpty(seatNumber) || string.IsNullOrEmpty(fullName))
                {
                    result.SkippedMissingData++;
                    AddError(result, rowNumber, seatNumber, "رقم جلوس أو اسم مفقود");
                    continue;
                }

                if (!TryParseDecimal(scoreRaw, out var totalScore) || totalScore < 0)
                {
                    result.SkippedMissingData++;
                    AddError(result, rowNumber, seatNumber, "الدرجة مفقودة أو غير صحيحة");
                    continue;
                }

                if (!seenInFile.Add(seatNumber))
                {
                    result.SkippedDuplicateInFile++;
                    AddError(result, rowNumber, seatNumber, "رقم جلوس مكرر في نفس الملف");
                    continue;
                }

                if (existingSeats.Contains(seatNumber))
                {
                    result.SkippedAlreadyInDb++;
                    AddError(result, rowNumber, seatNumber, "رقم جلوس مسجّل من قبل في النظام");
                    continue;
                }

                var percentage = Math.Round(totalScore / model.MaxScore * 100m, 2);

                if (percentage > 100m)
                {
                    if (model.AbortOnAnyOverflow)
                    {
                        await transaction.RollbackAsync();
                        result.Aborted = true;
                        result.AbortReason =
                            $"النهاية العظمى المُدخلة ({model.MaxScore}) غير مناسبة لهذا الملف. " +
                            $"رقم الجلوس {seatNumber} درجته {totalScore} ونسبته {percentage}% (> 100%). " +
                            "تم إلغاء الاستيراد بالكامل ولم تُحفظ أي بيانات.";
                        result.Duration = sw.Elapsed;
                        return ResultView(result);
                    }

                    result.SkippedOverMaxScore++;
                    AddError(result, rowNumber, seatNumber,
                        $"النسبة {percentage}% > 100% (MaxScore غير مناسب)");
                    continue;
                }

                var statusDescription = statusRaw?.ToString()?.Trim();
                var isEligible = !string.IsNullOrEmpty(statusDescription)
                    && statusDescription.Contains("ناجح");

                var row = batch.NewRow();
                row["SeatNumber"] = seatNumber;
                row["FullName"] = fullName;
                row["TotalScore"] = totalScore;
                row["MaxScore"] = model.MaxScore;
                row["Percentage"] = percentage;
                row["EquivalentPercentage"] = percentage; // Egyptian high school: same value
                row["StatusDescription"] = (object?)statusDescription ?? DBNull.Value;
                row["IsEligible"] = isEligible;
                row["NationalId"] = DBNull.Value;
                row["ImportedAt"] = DateTime.UtcNow;
                row["ImportBatch"] = result.ImportBatch!;
                batch.Rows.Add(row);

                if (isEligible) result.Imported++;
                else result.NotEligibleImported++;

                if (batch.Rows.Count >= bulkCopy.BatchSize)
                {
                    await bulkCopy.WriteToServerAsync(batch);
                    batch.Clear();
                }
            }

            if (batch.Rows.Count > 0)
            {
                await bulkCopy.WriteToServerAsync(batch);
            }

            await transaction.CommitAsync();
            result.Duration = sw.Elapsed;

            await LogAuditAsync("Import Official Records",
                $"Batch={result.ImportBatch}, MaxScore={model.MaxScore}, " +
                $"Read={result.TotalRowsRead}, Imported={result.Imported + result.NotEligibleImported} " +
                $"(eligible {result.Imported}, ineligible {result.NotEligibleImported})");

            return ResultView(result);
        }
        catch (Exception ex)
        {
            try { await transaction.RollbackAsync(); } catch { /* connection already broken */ }
            _logger.LogError(ex, "Failed to import official records");
            result.Aborted = true;
            result.AbortReason = "حدث خطأ أثناء تنفيذ العملية. يرجى المحاولة مرة أخرى.";
            result.Duration = sw.Elapsed;
            return ResultView(result);
        }
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

    private static void AddError(ImportOfficialRecordsResultViewModel result,
        int rowNumber, string? seatNumber, string reason)
    {
        if (result.FirstErrors.Count >= 50) return;
        result.FirstErrors.Add(new ImportRowError
        {
            RowNumber = rowNumber,
            SeatNumber = seatNumber,
            Reason = reason
        });
    }

    private static bool TryParseDecimal(object? value, out decimal result)
    {
        result = 0m;
        if (value is null) return false;
        if (value is decimal d) { result = d; return true; }
        if (value is double dbl) { result = (decimal)dbl; return true; }
        if (value is float f) { result = (decimal)f; return true; }
        if (value is int i) { result = i; return true; }
        if (value is long l) { result = l; return true; }
        return decimal.TryParse(value.ToString(),
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out result);
    }

    private static DataTable CreateOfficialRecordsTable()
    {
        var dt = new DataTable();
        dt.Columns.Add("SeatNumber", typeof(string));
        dt.Columns.Add("FullName", typeof(string));
        dt.Columns.Add("TotalScore", typeof(decimal));
        dt.Columns.Add("MaxScore", typeof(decimal));
        dt.Columns.Add("Percentage", typeof(decimal));
        dt.Columns.Add("EquivalentPercentage", typeof(decimal));
        dt.Columns.Add("StatusDescription", typeof(string));
        dt.Columns.Add("IsEligible", typeof(bool));
        dt.Columns.Add("NationalId", typeof(string));
        dt.Columns.Add("ImportedAt", typeof(DateTime));
        dt.Columns.Add("ImportBatch", typeof(string));
        return dt;
    }

    private static void ConfigureBulkCopyMappings(SqlBulkCopy bulkCopy)
    {
        bulkCopy.ColumnMappings.Add("SeatNumber", "SeatNumber");
        bulkCopy.ColumnMappings.Add("FullName", "FullName");
        bulkCopy.ColumnMappings.Add("TotalScore", "TotalScore");
        bulkCopy.ColumnMappings.Add("MaxScore", "MaxScore");
        bulkCopy.ColumnMappings.Add("Percentage", "Percentage");
        bulkCopy.ColumnMappings.Add("EquivalentPercentage", "EquivalentPercentage");
        bulkCopy.ColumnMappings.Add("StatusDescription", "StatusDescription");
        bulkCopy.ColumnMappings.Add("IsEligible", "IsEligible");
        bulkCopy.ColumnMappings.Add("NationalId", "NationalId");
        bulkCopy.ColumnMappings.Add("ImportedAt", "ImportedAt");
        bulkCopy.ColumnMappings.Add("ImportBatch", "ImportBatch");
    }

    // ================================
    // HELPER METHODS
    // ================================

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
