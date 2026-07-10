namespace ANU_Admissions.ViewModels;

/// <summary>
/// Display-only model for the student's official nomination result and the
/// printable nomination card. All values are read from the linked
/// OfficialStudentRecord and the Allocation — never from user input.
/// </summary>
public class NominationResultViewModel
{
    public bool IsAllocated { get; set; }

    // Official identity (from OfficialStudentRecord, falls back to account name)
    public string OfficialFullName { get; set; } = string.Empty;
    public string NationalId { get; set; } = string.Empty;
    public string SeatNumber { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    public decimal EquivalentPercentage { get; set; }

    // Allocation result
    public string CollegeName { get; set; } = string.Empty;
    public decimal FinalCutoff { get; set; }
    public DateTime AllocationDate { get; set; }
}
