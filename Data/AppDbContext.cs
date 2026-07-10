using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ANU_Admissions.Models;

namespace ANU_Admissions.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }
    
    public DbSet<StudentProfile> StudentProfiles { get; set; }
    public DbSet<College> Colleges { get; set; }
    public DbSet<Preference> Preferences { get; set; }
    public DbSet<Allocation> Allocations { get; set; }
    public DbSet<Document> Documents { get; set; }
    public DbSet<SystemSetting> SystemSettings { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<OfficialStudentRecord> OfficialStudentRecords { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Preference relationships
        modelBuilder.Entity<Preference>()
            .HasOne(p => p.StudentProfile)
            .WithMany(s => s.Preferences)  // ← Explicit navigation property
            .HasForeignKey(p => p.StudentProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Preference>()
            .HasOne(p => p.College)
            .WithMany(c => c.Preferences)
            .HasForeignKey(p => p.CollegeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Allocation relationships
        modelBuilder.Entity<Allocation>()
            .HasOne(a => a.StudentProfile)
            .WithMany(s => s.Allocations)  // ← Explicit navigation property
            .HasForeignKey(a => a.StudentProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Allocation>()
            .HasOne(a => a.College)
            .WithMany(c => c.Allocations)
            .HasForeignKey(a => a.CollegeId)
            .OnDelete(DeleteBehavior.Restrict);
        
        // Unique constraint: One allocation per student
        modelBuilder.Entity<Allocation>()
            .HasIndex(a => a.StudentProfileId)
            .IsUnique();

        // Document relationships
        modelBuilder.Entity<Document>()
            .HasOne(d => d.StudentProfile)
            .WithMany(s => s.Documents)
            .HasForeignKey(d => d.StudentProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique constraints for StudentProfile
        modelBuilder.Entity<StudentProfile>()
            .HasIndex(s => s.NationalId)
            .IsUnique()
            .HasFilter("[NationalId] IS NOT NULL");

        modelBuilder.Entity<StudentProfile>()
            .HasIndex(s => s.PhoneNumber)
            .IsUnique()
            .HasFilter("[PhoneNumber] IS NOT NULL");

        // College seed data
        modelBuilder.Entity<College>().HasData(
            new College { Id = 1, Code = "MED", NameAr = "كلية الطب", NameEn = "Faculty of Medicine", MinimumScore = 95.0m, Capacity = 200, AllowedSections = "علمي علوم" },
            new College { Id = 2, Code = "PHARM", NameAr = "كلية الصيدلة", NameEn = "Faculty of Pharmacy", MinimumScore = 93.0m, Capacity = 150, AllowedSections = "علمي علوم" },
            new College { Id = 3, Code = "DENT", NameAr = "كلية طب الأسنان", NameEn = "Faculty of Dentistry", MinimumScore = 94.0m, Capacity = 120, AllowedSections = "علمي علوم" },
            new College { Id = 4, Code = "ENG", NameAr = "كلية الهندسة", NameEn = "Faculty of Engineering", MinimumScore = 90.0m, Capacity = 300, AllowedSections = "علمي رياضة" },
            new College { Id = 5, Code = "CS", NameAr = "كلية الحاسبات والذكاء الاصطناعي", NameEn = "Faculty of Computer Science and AI", MinimumScore = 88.0m, Capacity = 250, AllowedSections = "علمي رياضة,علمي علوم" },
            new College { Id = 6, Code = "BUS", NameAr = "كلية إدارة الأعمال", NameEn = "Faculty of Business Administration", MinimumScore = 75.0m, Capacity = 400, AllowedSections = "علمي رياضة,علمي علوم,أدبي" },
            new College { Id = 7, Code = "ALSUN", NameAr = "كلية الألسن", NameEn = "Faculty of Languages", MinimumScore = 70.0m, Capacity = 300, AllowedSections = "علمي رياضة,علمي علوم,أدبي" },
            new College { Id = 8, Code = "FIN", NameAr = "كلية المالية والإدارة", NameEn = "Faculty of Finance and Administration", MinimumScore = 73.0m, Capacity = 350, AllowedSections = "علمي رياضة,علمي علوم,أدبي" }
        );

        // Unique constraint on Preferences
        modelBuilder.Entity<Preference>()
            .HasIndex(p => new { p.StudentProfileId, p.Rank })
            .IsUnique();

        // Decimal precision configuration
        modelBuilder.Entity<College>()
            .Property(c => c.MinimumScore)
            .HasPrecision(5, 2);
            
        modelBuilder.Entity<College>()
            .Property(c => c.FinalCutoff)
            .HasPrecision(5, 2);

        modelBuilder.Entity<StudentProfile>()
            .Property(s => s.TotalScore)
            .HasPrecision(5, 2);

        modelBuilder.Entity<StudentProfile>()
            .Property(s => s.Percentage)
            .HasPrecision(5, 2);

        modelBuilder.Entity<StudentProfile>()
            .Property(s => s.EquivalentPercentage)
            .HasPrecision(5, 2);

        // One-to-one: StudentProfile -> OfficialStudentRecord.
        // OfficialRecordId is unique so the same official record cannot be
        // linked to more than one profile. SetNull on delete keeps profiles
        // alive if an official record is removed during a re-import.
        modelBuilder.Entity<StudentProfile>()
            .HasOne(s => s.OfficialRecord)
            .WithOne(o => o.StudentProfile)
            .HasForeignKey<StudentProfile>(s => s.OfficialRecordId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<StudentProfile>()
            .HasIndex(s => s.OfficialRecordId)
            .IsUnique()
            .HasFilter("[OfficialRecordId] IS NOT NULL");

        // OfficialStudentRecord configuration
        modelBuilder.Entity<OfficialStudentRecord>()
            .HasIndex(o => o.SeatNumber)
            .IsUnique();

        modelBuilder.Entity<OfficialStudentRecord>()
            .HasIndex(o => o.NationalId)
            .IsUnique()
            .HasFilter("[NationalId] IS NOT NULL");

        modelBuilder.Entity<OfficialStudentRecord>()
            .Property(o => o.TotalScore)
            .HasPrecision(7, 3);

        modelBuilder.Entity<OfficialStudentRecord>()
            .Property(o => o.MaxScore)
            .HasPrecision(7, 3);

        modelBuilder.Entity<OfficialStudentRecord>()
            .Property(o => o.Percentage)
            .HasPrecision(5, 2);

        modelBuilder.Entity<OfficialStudentRecord>()
            .Property(o => o.EquivalentPercentage)
            .HasPrecision(5, 2);

        // Seed default system settings
        modelBuilder.Entity<SystemSetting>().HasData(
            new SystemSetting
            {
                Id = 1,
                Key = "AdmissionsOpen",
                Value = "true",
                LastModified = DateTime.UtcNow,
                ModifiedBy = "System"
            }
        );
    }
}
