using System.Text;
using LinerNotes.Application.Common.Interfaces;
namespace LinerNotes.Infrastructure.Email;

public sealed class EmailPayloadProtection(ILocalEmailFileProtection protection) : IEmailPayloadProtection
{
    private const string Prefix = "outbound-email-v1:";
    public void Prepare() => _ = Protect("key-preparation");
    public string Protect(string value) => Convert.ToBase64String(protection.Protect(Encoding.UTF8.GetBytes(Prefix + value)));
    public string Unprotect(string value)
    {
        var text = Encoding.UTF8.GetString(protection.Unprotect(Convert.FromBase64String(value)));
        return text.StartsWith(Prefix, StringComparison.Ordinal) ? text[Prefix.Length..] : throw new System.Security.Cryptography.CryptographicException("email_payload_purpose_invalid");
    }
}
