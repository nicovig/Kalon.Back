using System.Security.Claims;
using Kalon.Back.Configuration;
using Kalon.Back.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Kalon.Back.Tests;

public class PlanServiceTests
{
    private static PlanService CreateService(params Claim[] claims)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
        };

        return new PlanService(
            new HttpContextAccessor { HttpContext = httpContext },
            Options.Create(new PlanOptions
            {
                MaxDocumentsApplicationFeatureValue = "max_annual_documents",
                MaxEmailsApplicationFeatureValue = "max_annual_emails",
                MaxContactsApplicationFeatureValue = "max_contacts",
                ArchiveApplicationFeatureValue = "annual_archives",
                IaMailApplicationFeatureValue = "mail_ai",
                DonorsSearchApplicationFeatureValueCountLimit = "monthly_donors_search_limit",
                DonorsSearchApplicationFeatureValueCost = "donors_search_cost",
                DonorsDetailsApplicationFeatureValueCost = "donors_detail_cost"
            }));
    }

    [Fact]
    public void MaxDocumentsAnnual_ReadsValue_FromPlanFeaturesJsonObject()
    {
        var service = CreateService(
            new Claim("plan_features", "{\"max_annual_documents\":\"100\"}"));

        var result = service.MaxDocumentsAnnual;

        Assert.Equal(100, result);
    }

    [Fact]
    public void MaxDocumentsAnnual_ReadsValue_FromDoubleSerializedPlanFeatures()
    {
        var service = CreateService(
            new Claim("plan_features", "\"{\\\"max_annual_documents\\\":\\\"120\\\"}\""));

        var result = service.MaxDocumentsAnnual;

        Assert.Equal(120, result);
    }

    [Fact]
    public void MaxDocumentsAnnual_FallsBackToDirectClaim_WhenPlanFeaturesMissing()
    {
        var service = CreateService(
            new Claim("max_annual_documents", "130"));

        var result = service.MaxDocumentsAnnual;

        Assert.Equal(130, result);
    }

    [Fact]
    public void MaxDocumentsAnnual_ParsesEscapedQuotedNumber()
    {
        var service = CreateService(
            new Claim("plan_features", "{\"max_annual_documents\":\"\\\"999999\\\"\"}"));

        var result = service.MaxDocumentsAnnual;

        Assert.Equal(999999, result);
    }

    [Fact]
    public void PlanName_ReturnsFree_WhenClaimMissing()
    {
        var service = CreateService();

        var result = service.PlanName;

        Assert.Equal("Free", result);
    }
}
