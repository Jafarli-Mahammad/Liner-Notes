using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinerNotes.Presentation.Controllers;

[ApiController, AllowAnonymous, Route("api/public-information")]
public sealed class PublicInformationController(IConfiguration configuration) : ControllerBase
{
    [HttpGet]
    public IActionResult Read() => Ok(new
    {
        operatorName = configuration["PublicService:OperatorName"],
        privacyContact = configuration["PublicService:PrivacyContact"],
        emailEnabled = configuration.GetValue<bool>("EmailDelivery:Enabled"),
        mailProcessor = configuration.GetValue<bool>("EmailDelivery:Enabled") ? "Resend" : null,
        policyVersion = "2026-11-13"
    });
}
