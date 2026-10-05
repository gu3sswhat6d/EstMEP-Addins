using System.Linq;
using System.Text;
using EstMepAddins.DimensionChecker.Models;

namespace EstMepAddins.DimensionChecker.Verification
{
    /// <summary>Plain-text rendering of a report, for the results window and the saved file.</summary>
    public static class ReportFormatter
    {
        public static string Headline(VerificationReport report)
        {
            switch (report.OverallStatus)
            {
                case "pass": return "DIMENSIONALLY CORRECT";
                case "fail": return "DIMENSIONS DO NOT MATCH THE SUBMITTAL";
                default: return "COULD NOT VERIFY";
            }
        }

        public static string FixesText(VerificationReport report)
        {
            if (report.Fixes.Count == 0)
                return report.Passed ? "No changes needed." : "No specific fixes were returned.";

            var sb = new StringBuilder();
            var n = 1;
            foreach (var group in report.Fixes.GroupBy(f => f.SizeName))
            {
                sb.AppendLine("Size " + group.Key + ":");
                foreach (var fix in group)
                {
                    sb.AppendLine($"  {n++}. {fix.ItmDimension}: {fix.CurrentValue} -> {fix.RequiredValue}");
                    sb.AppendLine("     " + fix.Instruction);
                }
                sb.AppendLine();
            }
            return sb.ToString().TrimEnd();
        }

        public static string FullText(VerificationReport report, ItemSnapshot snapshot, string submittalPath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("ESTmep submittal dimension check");
            sb.AppendLine("ITM:        " + snapshot.ItemPath);
            sb.AppendLine("Submittal:  " + submittalPath);
            sb.AppendLine("Product:    " + report.SubmittalProduct);
            sb.AppendLine("Checked by: " + report.Model);
            sb.AppendLine();
            sb.AppendLine("Result: " + Headline(report));
            sb.AppendLine(report.Summary);
            sb.AppendLine();

            foreach (var size in report.Sizes)
            {
                sb.AppendLine($"[{size.Status.ToUpperInvariant()}] {size.SizeName}");
                foreach (var check in size.Checks)
                {
                    var label = string.IsNullOrEmpty(check.SubmittalLabel) ? "" : $" ({check.SubmittalLabel})";
                    sb.AppendLine($"    {check.Status,-9} {check.ItmDimension}{label}: ITM {check.ItmValue} / submittal {check.SubmittalValue}");
                    if (!string.IsNullOrEmpty(check.Note))
                        sb.AppendLine("              " + check.Note);
                }
            }

            if (report.SizesMissingFromItm.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Sizes on the submittal that the ITM does not have: " + string.Join(", ", report.SizesMissingFromItm));
            }

            sb.AppendLine();
            sb.AppendLine("How to fix:");
            sb.AppendLine(FixesText(report));
            return sb.ToString();
        }
    }
}
