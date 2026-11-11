using IntegrationHub.Models.Entities;

namespace IntegrationHub.Application.Contracts;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string passwordHash);
}

public interface ITokenIssuer
{
    string CreateToken(User user, DateTime expiresAt);
}
