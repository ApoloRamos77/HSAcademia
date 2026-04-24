namespace HSAcademia.Application.DTOs.SuperAdmin;

// ── Registration Requests ────────────────────────────────────────────────

public class SubmitRegistrationRequestDto
{
    public string AcademyName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string? ContactPhone { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? Sport { get; set; }
    public string? Website { get; set; }
    public string? AdditionalInfo { get; set; }
}

public class RegistrationRequestListDto
{
    public Guid Id { get; set; }
    public string AcademyName { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string? ContactPhone { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? Sport { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
}

public class RegistrationRequestDetailDto : RegistrationRequestListDto
{
    public string? Description { get; set; }
    public string? Website { get; set; }
    public string? AdditionalInfo { get; set; }
    public string? ReviewNotes { get; set; }
    public Guid? CreatedAcademyId { get; set; }
}

public class ApproveRequestDto
{
    public string? ReviewNotes { get; set; }
}

public class RejectRequestDto
{
    public string Reason { get; set; } = string.Empty;
}

// ── Academy ──────────────────────────────────────────────────────────────

public class AcademyCreatedDto
{
    public Guid AcademyId { get; set; }
    public Guid AcademyTenantId { get; set; }
    public string SlugName { get; set; } = string.Empty;
    public Guid AdminUserId { get; set; }
    public string AdminEmail { get; set; } = string.Empty;
    public string TempPassword { get; set; } = string.Empty;
}

public class AcademyListDto
{
    public Guid Id { get; set; }
    public Guid AcademyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SlugName { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? Sport { get; set; }
    public string Status { get; set; } = string.Empty;
    public int UsersCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
}

public class AcademyDetailDto : AcademyListDto
{
    public string? Description { get; set; }
    public string? ContactPhone { get; set; }
    public string? Website { get; set; }
    public string? SuspensionReason { get; set; }
    public DateTime? SuspendedAt { get; set; }
    public List<UserSummaryDto> Users { get; set; } = new();
}

public class SuspendAcademyDto
{
    public string Reason { get; set; } = string.Empty;
}

public class DeactivateAcademyDto
{
    public string Reason { get; set; } = string.Empty;
}

// ── Users ─────────────────────────────────────────────────────────────────

public class UserListDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Guid? AcademyId { get; set; }
    public string? AcademyName { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UserSummaryDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class SuspendUserDto
{
    public string Reason { get; set; } = string.Empty;
}

public class DeactivateUserDto
{
    public string Reason { get; set; } = string.Empty;
}

// ── Dashboard ─────────────────────────────────────────────────────────────

public class DashboardStatsDto
{
    public int TotalAcademies { get; set; }
    public int ActiveAcademies { get; set; }
    public int SuspendedAcademies { get; set; }
    public int PendingRequests { get; set; }
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
}
