namespace FengDeskAI.Application.Interfaces.Security;

public interface IPasswordService
{
    string Hash(string password);
    bool Verify(string password, string passwordHash);
}
