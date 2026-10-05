using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Anthropic;
using Anthropic.Models.Beta.Messages;
using EstMepAddins.DimensionChecker.Models;

namespace EstMepAddins.DimensionChecker.Verification
{
    public sealed class VerificationOptions
    {
        public const string DefaultModel = "claude-opus-5-5";

        public string Model { get; set; } = DefaultModel;

        /// <summary>Free text passed to Claude, e.g. "1/16 in" or "1 mm".</summary>
        public string Tolerance { get; set; } = "1/16 in";
    }

    /// <summary>
    /// Sends the submittal PDF and the ITM snapshot to Claude and returns its structured verdict.
    /// </summary>
    public sealed class ClaudeSubmittalVerifier
    {
        // Submittals are sent inline as base64; the API caps a request at 32 MB, and base64 adds a third.
        public const long MaxPdfBytes = 23L * 1024 * 1024;

        // Server-side fallback: if the model declines for policy reasons, the API retries on its
        // default fallback model inside the same call instead of failing the check.
        private const string FallbackBeta = "server-side-fallback-2026-07-01";

        private readonly AnthropicClient _client;

        /// <param name="httpClient">Optional; lets tests capture requests without a network.</param>
        public ClaudeSubmittalVerifier(string apiKey, HttpClient? httpClient = null)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("An Anthropic API key is required.", nameof(apiKey));

            _client = httpClient == null
                ? new AnthropicClient { ApiKey = apiKey }
                : new AnthropicClient { ApiKey = apiKey, HttpClient = httpClient };
        }

        public async Task<VerificationReport> VerifyAsync(
            ItemSnapshot snapshot,
            byte[] submittalPdf,
            VerificationOptions options,
            CancellationToken cancellationToken = default)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (submittalPdf == null || submittalPdf.Length == 0)
                throw new ArgumentException("The submittal PDF is empty.", nameof(submittalPdf));
            if (submittalPdf.Length > MaxPdfBytes)
                throw new ArgumentException(
                    $"The submittal PDF is {submittalPdf.Length / (1024 * 1024)} MB; the limit is {MaxPdfBytes / (1024 * 1024)} MB. " +
                    "Split it so only the pages for this product are included.");

            var parameters = new MessageCreateParams
            {
                Model = options.Model,
                MaxTokens = 64000,
                Betas = [FallbackBeta],
                Fallbacks = new Default(),
                Thinking = new BetaThinkingConfigAdaptive(),
                OutputConfig = new BetaOutputConfig
                {
                    // Opus 5.5 defaults to medium; reading dimension tables is worth the extra care.
                    Effort = Effort.High,
                    Format = new BetaJsonOutputFormat { Schema = ReportSchema.AsDictionary() },
                },
                System = PromptBuilder.SystemPrompt,
                Messages =
                [
                    new BetaMessageParam
                    {
                        Role = Role.User,
                        Content = new List<BetaContentBlockParam>
                        {
                            new BetaRequestDocumentBlock
                            {
                                Source = new BetaBase64PdfSource { Data = Convert.ToBase64String(submittalPdf) },
                                Title = "Submittal",
                            },
                            new BetaTextBlockParam
                            {
                                Text = PromptBuilder.BuildUserText(snapshot, options.Tolerance),
                            },
                        },
                    },
                ],
            };

            // Streamed so a long thinking pass over a big submittal can't hit the HTTP timeout.
            var text = new StringBuilder();
            string? stopReason = null;
            await foreach (var streamEvent in _client.Beta.Messages.CreateStreaming(parameters, cancellationToken))
            {
                if (streamEvent.TryPickContentBlockDelta(out var blockDelta) &&
                    blockDelta.Delta.TryPickText(out var textDelta))
                {
                    text.Append(textDelta.Text);
                }
                else if (streamEvent.TryPickDelta(out var messageDelta) && messageDelta.Delta.StopReason != null)
                {
                    stopReason = messageDelta.Delta.StopReason.Raw();
                }
            }

            if (stopReason == "refusal")
                throw new VerificationException("Claude declined to review this submittal.");
            if (stopReason == "max_tokens")
                throw new VerificationException(
                    "The answer was cut off before it finished. Try a submittal with only the pages for this product.");

            var report = ParseReport(text.ToString());
            report.Model = options.Model;
            return report;
        }

        public static VerificationReport ParseReport(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new VerificationException("Claude returned an empty answer.");

            try
            {
                var report = JsonSerializer.Deserialize<VerificationReport>(json);
                if (report == null)
                    throw new VerificationException("Claude returned an empty answer.");
                return report;
            }
            catch (JsonException ex)
            {
                throw new VerificationException("Claude's answer was not valid JSON: " + ex.Message, ex);
            }
        }
    }

    public sealed class VerificationException : Exception
    {
        public VerificationException(string message) : base(message) { }
        public VerificationException(string message, Exception inner) : base(message, inner) { }
    }
}
