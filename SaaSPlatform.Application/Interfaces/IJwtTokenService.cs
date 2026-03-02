using SaaSPlatform.Domain.Entities;

namespace SaaSPlatform.Application.Interfaces;

public interface IJwtTokenService
{
    string CreateToken(User user);
}