using InsightMail.API.Models;
using InsightMail.API.Services;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/test")]
public class TestController : ControllerBase
{
    private readonly IClassifierService _classifier;

    public TestController(IClassifierService classifier)
    {
        _classifier = classifier;
    }

    [HttpGet("classification")]
    public async Task<ActionResult> Test()
    {
        var email = new Email
        {
            Subject = "URGENT: Server Down",
            Body = "Production server crashed and customers cannot login."
        };

        var result = await _classifier.ClassifyEmailAsync(email);

        return Ok(result);
    }
}