using System.Text.RegularExpressions;

namespace ServiceDesk.LiveIntegration;

internal static class RunIsolation
{
    public static void Validate(string runId, string root, string reportPath, string composePath)
    {
        if (!Regex.IsMatch(runId, "^sdverify[0-9a-f]{32}$")) throw new InvalidOperationException("Invalid isolated run identity.");
        var expectedReport = Path.GetFullPath(Path.Combine(root, "test-results", "LiveIntegration", runId, "report.json"));
        var expectedCompose = Path.GetFullPath(Path.Combine(root, "tests", "LiveIntegration", "compose.yaml"));
        if (!string.Equals(expectedReport, Path.GetFullPath(reportPath), StringComparison.OrdinalIgnoreCase)
            || !string.Equals(expectedCompose, Path.GetFullPath(composePath), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Infrastructure and reports must belong to this verification workspace.");
    }
}
