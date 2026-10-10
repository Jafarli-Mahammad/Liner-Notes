using System.Net;
using System.Net.Mail;

namespace LinerNotes.Infrastructure.Email;

public sealed class LocalEmailOptions
{
    public bool Enabled { get; set; }
    public string? ApplicationOrigin { get; set; }
    public string? SinkRoot { get; set; }
    public string FromAddress { get; set; } = "liner-notes@localhost";
    public string MessageDomain { get; set; } = "localhost";
    public int AlternativeByteLimit { get; set; } = 100_000;
    public int MessageByteLimit { get; set; } = 400_000;

    public Uri Origin()
    {
        if (!Enabled || !Uri.TryCreate(ApplicationOrigin, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") || !IPAddress.TryParse(uri.Host, out var address) || !IPAddress.IsLoopback(address) ||
            uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.AbsolutePath != "/")
            throw new InvalidOperationException("local_email_origin_required");
        return uri;
    }

    public void Validate()
    {
        Origin();
        ValidateAddress(FromAddress);
        if (string.IsNullOrEmpty(MessageDomain) || MessageDomain.Length > 253 ||
            MessageDomain.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '.' or '-')) ||
            AlternativeByteLimit is <= 0 or > 100_000 || MessageByteLimit is <= 0 or > 400_000)
            throw new InvalidOperationException("local_email_configuration_invalid");
    }

    public static void ValidateAddress(string value)
    {
        if (value.Length > 256 || value.Any(char.IsControl) || !MailAddress.TryCreate(value, out var mail) || mail.Address != value)
            throw new InvalidOperationException("local_email_address_invalid");
    }
}
