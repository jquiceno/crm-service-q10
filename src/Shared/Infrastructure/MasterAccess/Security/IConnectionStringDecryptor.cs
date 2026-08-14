using Shared.Results;

namespace Infrastructure.MasterAccess.Security;

public interface IConnectionStringDecryptor
{
    Result<string> Decrypt(string cipherText);
}
