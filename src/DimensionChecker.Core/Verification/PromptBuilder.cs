using System.Text;
using System.Text.Json;
using EstMepAddins.DimensionChecker.Models;

namespace EstMepAddins.DimensionChecker.Verification
{
    public static class PromptBuilder
    {
        public const string SystemPrompt =
@"You check Autodesk Fabrication ESTmep items (ITMs) against manufacturer submittals for an MEP estimator.

You receive the submittal PDF and a JSON snapshot of one ITM: every size in its product list, with the item dimensions, options and product list data ESTmep reported after that size was loaded and drawn.

How to compare:
- Find the dimension table(s) and drawings in the submittal for this product. Map each submittal dimension label (letters like A/B/C, or names like ""Overall Length"", ""Center to End"", ""Socket Depth"") to the ITM dimension that represents the same physical measurement. Use the drawing, the product type and the values themselves to decide the mapping, and say in the note how you mapped it when it is not obvious.
- Match ITM sizes to submittal rows by nominal size. Size names may be formatted differently (""2in"" vs 2"" vs ""DN50"").
- Convert units before comparing. Treat values within the given tolerance as a match. Fractions such as 1-1/16"" are exact values.
- Only check dimensions that exist on both sides. An ITM dimension with no submittal counterpart is not a failure; leave it out unless it looks like it should have one.
- A size in the ITM with no row in the submittal gets status ""not_in_submittal"". Submittal sizes missing from the ITM go in sizes_missing_from_itm.
- overall_status is ""pass"" only if every compared dimension matches. Use ""inconclusive"" if the submittal does not have a readable dimension table for this product or is for a different product; explain why in the summary.

For every mismatch add one entry to fixes. The instruction must tell an ESTmep user exactly what to change: open the ITM in ESTmep's item editor, which tab (Product List / Dimensions / Options), which row (size) and which column or dimension, and the value to enter, in the ITM's units. If the dimension is not in the product list, say to change it on the Dimensions tab with that size selected. If several sizes share the same error pattern (e.g. every size is off by the same amount), say so in the summary.

Keep the summary to two or three plain sentences an estimator can act on.";

        public static string BuildUserText(ItemSnapshot snapshot, string tolerance)
        {
            var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });

            var sb = new StringBuilder();
            sb.AppendLine("Verify this ESTmep item against the attached submittal.");
            sb.AppendLine();
            sb.AppendLine("Tolerance: " + tolerance);
            sb.AppendLine("ITM units: " + snapshot.Units);
            sb.AppendLine();
            sb.AppendLine("<itm_snapshot>");
            sb.AppendLine(json);
            sb.AppendLine("</itm_snapshot>");
            return sb.ToString();
        }
    }
}
