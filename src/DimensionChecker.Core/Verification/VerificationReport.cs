using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace EstMepAddins.DimensionChecker.Verification
{
    /// <summary>
    /// Claude's verdict, deserialized from the JSON that <see cref="ReportSchema"/> constrains it to.
    /// </summary>
    public sealed class VerificationReport
    {
        /// <summary>"pass", "fail" or "inconclusive".</summary>
        [JsonPropertyName("overall_status")]
        public string OverallStatus { get; set; } = "";

        [JsonPropertyName("summary")]
        public string Summary { get; set; } = "";

        /// <summary>What Claude identified the submittal as (manufacturer, model / series).</summary>
        [JsonPropertyName("submittal_product")]
        public string SubmittalProduct { get; set; } = "";

        [JsonPropertyName("sizes")]
        public List<SizeResult> Sizes { get; set; } = new List<SizeResult>();

        [JsonPropertyName("fixes")]
        public List<FixInstruction> Fixes { get; set; } = new List<FixInstruction>();

        /// <summary>Sizes listed on the submittal that the ITM's product list does not have.</summary>
        [JsonPropertyName("sizes_missing_from_itm")]
        public List<string> SizesMissingFromItm { get; set; } = new List<string>();

        [JsonIgnore]
        public bool Passed => OverallStatus == "pass";

        /// <summary>Set by the verifier, not by Claude: the model that produced the answer.</summary>
        [JsonIgnore]
        public string Model { get; set; } = "";
    }

    public sealed class SizeResult
    {
        [JsonPropertyName("size_name")]
        public string SizeName { get; set; } = "";

        /// <summary>"pass", "fail" or "not_in_submittal".</summary>
        [JsonPropertyName("status")]
        public string Status { get; set; } = "";

        [JsonPropertyName("checks")]
        public List<DimensionCheck> Checks { get; set; } = new List<DimensionCheck>();
    }

    public sealed class DimensionCheck
    {
        [JsonPropertyName("itm_dimension")]
        public string ItmDimension { get; set; } = "";

        /// <summary>The label the submittal uses for the same dimension (e.g. "A", "Overall Length").</summary>
        [JsonPropertyName("submittal_label")]
        public string SubmittalLabel { get; set; } = "";

        [JsonPropertyName("itm_value")]
        public string ItmValue { get; set; } = "";

        [JsonPropertyName("submittal_value")]
        public string SubmittalValue { get; set; } = "";

        /// <summary>"match", "mismatch" or "not_found".</summary>
        [JsonPropertyName("status")]
        public string Status { get; set; } = "";

        [JsonPropertyName("note")]
        public string Note { get; set; } = "";
    }

    public sealed class FixInstruction
    {
        [JsonPropertyName("size_name")]
        public string SizeName { get; set; } = "";

        [JsonPropertyName("itm_dimension")]
        public string ItmDimension { get; set; } = "";

        [JsonPropertyName("current_value")]
        public string CurrentValue { get; set; } = "";

        [JsonPropertyName("required_value")]
        public string RequiredValue { get; set; } = "";

        /// <summary>Step-by-step instruction for making the change in ESTmep's item editor.</summary>
        [JsonPropertyName("instruction")]
        public string Instruction { get; set; } = "";
    }
}
