using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using EstMepAddins.DimensionChecker.Models;
using EstMepAddins.DimensionChecker.Verification;
using Xunit;

namespace DimensionChecker.Core.Tests;

public class ClaudeSubmittalVerifierTests
{
    private const string FailingReportJson = """
        {"overall_status":"fail","summary":"The 2in size's center-to-end is 1/8 in short.","submittal_product":"Acme 90 Elbow",
         "sizes":[{"size_name":"2in","status":"fail","checks":[{"itm_dimension":"Length","submittal_label":"A","itm_value":"2.875 in","submittal_value":"3 in","status":"mismatch","note":""}]}],
         "fixes":[{"size_name":"2in","itm_dimension":"Length","current_value":"2.875 in","required_value":"3 in","instruction":"Product List tab, row 2in, set Length to 3."}],
         "sizes_missing_from_itm":["8in"]}
        """;

    private static ItemSnapshot SampleSnapshot() => new()
    {
        ItemName = "90 Elbow",
        ItemPath = @"C:\Items\Pipe\90 Elbow.ITM",
        Units = "inches",
        Sizes =
        {
            new ItemSizeSnapshot
            {
                SizeName = "2in",
                ProductListIndex = 0,
                Drawn = true,
                Dimensions = { new NamedValue("Length", "2.875") },
            },
        },
    };

    [Fact]
    public async Task Sends_pdf_snapshot_and_schema_and_parses_streamed_report()
    {
        var handler = new CapturingHandler(Sse(FailingReportJson, "end_turn"));
        var verifier = new ClaudeSubmittalVerifier("test-key", new HttpClient(handler));
        var pdf = Encoding.ASCII.GetBytes("%PDF-1.4 fake");

        var report = await verifier.VerifyAsync(SampleSnapshot(), pdf, new VerificationOptions { Tolerance = "1/16 in" });

        Assert.Equal("fail", report.OverallStatus);
        Assert.False(report.Passed);
        Assert.Equal("claude-opus-5-5", report.Model);
        Assert.Single(report.Fixes);
        Assert.Equal("3 in", report.Fixes[0].RequiredValue);
        Assert.Equal(new[] { "8in" }, report.SizesMissingFromItm);

        using var body = JsonDocument.Parse(handler.Body!);
        var root = body.RootElement;
        Assert.Equal("claude-opus-5-5", root.GetProperty("model").GetString());
        Assert.True(root.GetProperty("stream").GetBoolean());
        Assert.Equal("default", root.GetProperty("fallbacks").GetString());
        Assert.Equal("adaptive", root.GetProperty("thinking").GetProperty("type").GetString());
        var outputConfig = root.GetProperty("output_config");
        Assert.Equal("high", outputConfig.GetProperty("effort").GetString());
        Assert.Equal("json_schema", outputConfig.GetProperty("format").GetProperty("type").GetString());

        var content = root.GetProperty("messages")[0].GetProperty("content");
        var document = content[0];
        Assert.Equal("document", document.GetProperty("type").GetString());
        Assert.Equal("application/pdf", document.GetProperty("source").GetProperty("media_type").GetString());
        Assert.Equal(Convert.ToBase64String(pdf), document.GetProperty("source").GetProperty("data").GetString());
        var text = content[1].GetProperty("text").GetString()!;
        Assert.Contains("Tolerance: 1/16 in", text);
        Assert.Contains("\"SizeName\": \"2in\"", text);

        Assert.Contains("server-side-fallback-2026-07-01", handler.BetaHeader);
    }

    [Fact]
    public async Task Refusal_becomes_a_readable_error()
    {
        var verifier = new ClaudeSubmittalVerifier("test-key", new HttpClient(new CapturingHandler(Sse("", "refusal"))));

        var ex = await Assert.ThrowsAsync<VerificationException>(() =>
            verifier.VerifyAsync(SampleSnapshot(), new byte[] { 1 }, new VerificationOptions()));
        Assert.Contains("declined", ex.Message);
    }

    [Fact]
    public async Task Oversized_pdf_is_rejected_before_calling_the_api()
    {
        var handler = new CapturingHandler(Sse(FailingReportJson, "end_turn"));
        var verifier = new ClaudeSubmittalVerifier("test-key", new HttpClient(handler));

        await Assert.ThrowsAsync<ArgumentException>(() => verifier.VerifyAsync(
            SampleSnapshot(), new byte[ClaudeSubmittalVerifier.MaxPdfBytes + 1], new VerificationOptions()));
        Assert.Null(handler.Body);
    }

    [Fact]
    public void Formatter_lists_fixes_by_size()
    {
        var report = ClaudeSubmittalVerifier.ParseReport(FailingReportJson);

        Assert.Equal("DIMENSIONS DO NOT MATCH THE SUBMITTAL", ReportFormatter.Headline(report));
        var fixes = ReportFormatter.FixesText(report);
        Assert.Contains("Size 2in:", fixes);
        Assert.Contains("Length: 2.875 in -> 3 in", fixes);
        var full = ReportFormatter.FullText(report, SampleSnapshot(), @"C:\sub.pdf");
        Assert.Contains("[FAIL] 2in", full);
        Assert.Contains("8in", full);
    }

    [Fact]
    public void Schema_is_valid_json_with_strict_objects()
    {
        var schema = ReportSchema.AsDictionary();
        Assert.Equal("object", schema["type"].GetString());
        Assert.False(schema["additionalProperties"].GetBoolean());
    }

    private static string Sse(string reportJson, string stopReason)
    {
        var sb = new StringBuilder();
        void Event(string name, object data)
        {
            sb.Append("event: ").Append(name).Append('\n');
            sb.Append("data: ").Append(JsonSerializer.Serialize(data)).Append("\n\n");
        }

        Event("message_start", new
        {
            type = "message_start",
            message = new
            {
                id = "msg_test", type = "message", role = "assistant", model = "claude-opus-5-5",
                content = Array.Empty<object>(), stop_reason = (string?)null, stop_sequence = (string?)null,
                usage = new { input_tokens = 10, output_tokens = 1 },
            },
        });
        Event("content_block_start", new { type = "content_block_start", index = 0, content_block = new { type = "text", text = "" } });
        // Split the JSON across deltas the way a real stream would.
        for (var i = 0; i < reportJson.Length; i += 40)
        {
            var piece = reportJson.Substring(i, Math.Min(40, reportJson.Length - i));
            Event("content_block_delta", new { type = "content_block_delta", index = 0, delta = new { type = "text_delta", text = piece } });
        }
        Event("content_block_stop", new { type = "content_block_stop", index = 0 });
        Event("message_delta", new
        {
            type = "message_delta",
            delta = new { stop_reason = stopReason, stop_sequence = (string?)null },
            usage = new { output_tokens = 20 },
        });
        Event("message_stop", new { type = "message_stop" });
        return sb.ToString();
    }

    private sealed class CapturingHandler(string sseBody) : HttpMessageHandler
    {
        public string? Body { get; private set; }
        public string BetaHeader { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            if (request.Headers.TryGetValues("anthropic-beta", out var values))
                BetaHeader = string.Join(",", values);

            var content = new StringContent(sseBody, Encoding.UTF8);
            content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }
    }
}
