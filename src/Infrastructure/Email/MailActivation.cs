using LinerNotes.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
namespace LinerNotes.Infrastructure.Email;

public static class MailActivation
{
    public static void Validate(IConfiguration configuration, bool production)
    {
        if (production)
        {
            if (string.IsNullOrWhiteSpace(configuration["PublicService:OperatorName"]) ||
                !Uri.TryCreate(configuration["PublicService:PrivacyContact"], UriKind.Absolute, out var contact) ||
                contact.Scheme is not ("https" or "mailto") || contact.Scheme == "https" && contact.UserInfo.Length != 0 ||
                contact.Scheme == "mailto" && !System.Net.Mail.MailAddress.TryCreate(contact.OriginalString[7..], out _) ||
                string.IsNullOrWhiteSpace(configuration["PublicService:RightsReference"]) ||
                configuration["PublicService:MusicBrainzUse"] is not ("noncommercial" or "licensed") ||
                configuration["PublicService:MusicBrainzUse"] == "licensed" && string.IsNullOrWhiteSpace(configuration["PublicService:MusicBrainzLicenseReference"]))
                throw new InvalidOperationException("public_service_information_and_rights_required");
        }
        if (!configuration.GetValue<bool>("EmailDelivery:Enabled")) return;
        var options = configuration.GetSection("EmailDelivery").Get<EmailDeliveryOptions>() ?? new();
        options.Validate();
        if (configuration.GetValue<bool>("LocalEmail:Enabled")) throw new InvalidOperationException("email_delivery_modes_conflict");
        if (string.IsNullOrWhiteSpace(configuration["EmailDelivery:ApiKey"]) ||
            !configuration.GetValue<bool>("EmailDelivery:TrackingDisabledConfirmed") ||
            configuration["EmailDelivery:WebhookSecret"]?.StartsWith("whsec_", StringComparison.Ordinal) != true)
            throw new InvalidOperationException("email_provider_activation_required");
        try
        {
            if (Convert.FromBase64String(configuration["EmailDelivery:WebhookSecret"]![6..]).Length < 16)
                throw new InvalidOperationException("email_webhook_secret_invalid");
        }
        catch (FormatException) { throw new InvalidOperationException("email_webhook_secret_invalid"); }
        if (production && (!options.PublicOrigin.StartsWith("https://", StringComparison.Ordinal) ||
            options.PublicOrigin != configuration["GuestDiscovery:PublicOrigin"]))
            throw new InvalidOperationException("email_public_origin_mismatch");
    }
}
