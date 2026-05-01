using HSAcademia.Domain.Entities;

namespace HSAcademia.Application.Interfaces;

public interface IJwtService
{
    string GenerateToken(User user);
    Task<string> GenerateTokenAsync(User user);
    Guid? ValidateToken(string token);
}
