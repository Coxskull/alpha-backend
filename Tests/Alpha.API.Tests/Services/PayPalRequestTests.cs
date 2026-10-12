using System.Globalization;
using System.Net;
using System.Text.Json;
using Alpha.API.Services;
using Microsoft.Extensions.Configuration;
using Xunit;
namespace Alpha.API.Tests.Services;

public sealed class PayPalRequestTests
{
    [Fact]
    public async Task RefundUsesStableRequestIdRepresentationAndInvariantAmount()
    {
        var handler = new RecordingHandler();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["PayPal:ClientId"] = "synthetic", ["PayPal:ClientSecret"] = "synthetic", ["PayPal:BaseUrl"] = "https://provider.invalid" }).Build();
        var service = new PayPalService(new HttpClient(handler), config);
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            using var response = await service.RefundCapture("CAPTURE-1", 10.25m, "USD", "stable-request-id");
            Assert.Equal("stable-request-id", handler.RequestId);
            Assert.Equal("return=representation", handler.Prefer);
            using var body = JsonDocument.Parse(handler.Body!);
            Assert.Equal("10.25", body.RootElement.GetProperty("amount").GetProperty("value").GetString());
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
    private sealed class RecordingHandler : HttpMessageHandler
    {
        public string? RequestId, Prefer, Body;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath.Contains("oauth2"))
                return new(HttpStatusCode.OK) { Content = new StringContent("{\"access_token\":\"synthetic\"}") };
            RequestId = request.Headers.GetValues("PayPal-Request-Id").Single();
            Prefer = request.Headers.GetValues("Prefer").Single();
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new(HttpStatusCode.OK) { Content = new StringContent("{}") };
        }
    }
}
