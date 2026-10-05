using System.Collections.Generic;
using System.Text.Json;

namespace EstMepAddins.DimensionChecker.Verification
{
    /// <summary>
    /// JSON schema for structured output. Claude's response is constrained to this shape, which
    /// <see cref="VerificationReport"/> mirrors.
    /// </summary>
    public static class ReportSchema
    {
        public const string Json = @"{
  ""type"": ""object"",
  ""additionalProperties"": false,
  ""required"": [""overall_status"", ""summary"", ""submittal_product"", ""sizes"", ""fixes"", ""sizes_missing_from_itm""],
  ""properties"": {
    ""overall_status"": { ""type"": ""string"", ""enum"": [""pass"", ""fail"", ""inconclusive""] },
    ""summary"": { ""type"": ""string"" },
    ""submittal_product"": { ""type"": ""string"" },
    ""sizes"": {
      ""type"": ""array"",
      ""items"": {
        ""type"": ""object"",
        ""additionalProperties"": false,
        ""required"": [""size_name"", ""status"", ""checks""],
        ""properties"": {
          ""size_name"": { ""type"": ""string"" },
          ""status"": { ""type"": ""string"", ""enum"": [""pass"", ""fail"", ""not_in_submittal""] },
          ""checks"": {
            ""type"": ""array"",
            ""items"": {
              ""type"": ""object"",
              ""additionalProperties"": false,
              ""required"": [""itm_dimension"", ""submittal_label"", ""itm_value"", ""submittal_value"", ""status"", ""note""],
              ""properties"": {
                ""itm_dimension"": { ""type"": ""string"" },
                ""submittal_label"": { ""type"": ""string"" },
                ""itm_value"": { ""type"": ""string"" },
                ""submittal_value"": { ""type"": ""string"" },
                ""status"": { ""type"": ""string"", ""enum"": [""match"", ""mismatch"", ""not_found""] },
                ""note"": { ""type"": ""string"" }
              }
            }
          }
        }
      }
    },
    ""fixes"": {
      ""type"": ""array"",
      ""items"": {
        ""type"": ""object"",
        ""additionalProperties"": false,
        ""required"": [""size_name"", ""itm_dimension"", ""current_value"", ""required_value"", ""instruction""],
        ""properties"": {
          ""size_name"": { ""type"": ""string"" },
          ""itm_dimension"": { ""type"": ""string"" },
          ""current_value"": { ""type"": ""string"" },
          ""required_value"": { ""type"": ""string"" },
          ""instruction"": { ""type"": ""string"" }
        }
      }
    },
    ""sizes_missing_from_itm"": { ""type"": ""array"", ""items"": { ""type"": ""string"" } }
  }
}";

        public static Dictionary<string, JsonElement> AsDictionary()
        {
            using (var doc = JsonDocument.Parse(Json))
            {
                var result = new Dictionary<string, JsonElement>();
                foreach (var property in doc.RootElement.EnumerateObject())
                    result[property.Name] = property.Value.Clone();
                return result;
            }
        }
    }
}
