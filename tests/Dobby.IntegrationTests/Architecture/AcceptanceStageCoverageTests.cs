using System.Text.RegularExpressions;
using Shouldly;

namespace Dobby.IntegrationTests.Architecture;

/// <summary>
/// Documentation-shape guard for the `docs/07_ACCEPTANCE_CRITERIA.md` stage matrix.
///
/// <remarks>
/// NFR-027 / ADR-0061 boundary: these assertions scan document text, so they enforce only
/// the *shape* of the acceptance authority -- that every acceptance row has a stage
/// interpretation and that ids are unique and unbroken. They are deliberately NOT evidence
/// that any acceptance criterion is met, and they close no acceptance gate; each AC is
/// closed by its own behavior-first proof.
///
/// This guard exists because the stage matrix previously carried an end-anchored
/// enumeration (`AC-014`-`AC-053`) while the release blocker table had already grown to
/// `AC-054`. The newest -- and therefore least-reviewed -- acceptance row silently fell
/// outside every stage interpretation, so nothing declared whether it gated a release
/// candidate. A range that must be hand-edited on every addition is a recurring drift
/// surface, so the matrix now defines membership by the table itself and this test pins
/// that no acceptance row can ever again be stage-less.
/// </remarks>
/// </summary>
public sealed class AcceptanceStageCoverageTests
{
    private const string AcceptanceDocument = "docs/07_ACCEPTANCE_CRITERIA.md";

    [Fact]
    public void Every_acceptance_row_is_covered_by_a_stage_interpretation()
    {
        var text = ReadAcceptanceDocument();

        // The matrix must not reintroduce an end-anchored enumeration that has to be
        // hand-widened whenever an acceptance row is appended.
        text.Contains("Membership is defined by the table itself, not by this enumeration", StringComparison.Ordinal)
            .ShouldBeTrue(
                "the stage matrix must derive membership from the acceptance table; a bare " +
                "id range silently leaves every newly added AC without a stage");

        var declaredIds = AcceptanceIds(text);
        declaredIds.ShouldNotBeEmpty();

        // Any id named in an item-specific matrix row or a stage section counts as
        // explicitly interpreted; everything else is covered by the default table rule,
        // which the assertion above proves is present and open-ended.
        var highest = declaredIds.Max();
        text.Contains($"`AC-{highest:D3}`", StringComparison.Ordinal)
            .ShouldBeTrue(
                $"AC-{highest:D3} is the newest acceptance row, so the stage matrix must name " +
                "it in the current-membership parenthetical or in an item-specific note");
    }

    [Fact]
    public void Acceptance_ids_are_unique_and_contiguous()
    {
        var declaredIds = AcceptanceIds(ReadAcceptanceDocument());

        declaredIds.ShouldBeUnique(
            "a duplicated acceptance id makes traceability ambiguous: two rows would " +
            "answer to the same gate reference");

        var ordered = declaredIds.Order().ToArray();
        for (var index = 1; index < ordered.Length; index++)
        {
            ordered[index].ShouldBe(
                ordered[index - 1] + 1,
                $"acceptance ids must be contiguous; AC-{ordered[index - 1]:D3} is followed " +
                $"by AC-{ordered[index]:D3}, so a retired or renumbered row left a hole that " +
                "downstream references may still point at");
        }
    }

    private static int[] AcceptanceIds(string text) =>
        Regex.Matches(text, @"^\| AC-(\d{3}) \|", RegexOptions.Multiline)
            .Select(match => int.Parse(match.Groups[1].Value))
            .ToArray();

    private static string ReadAcceptanceDocument()
    {
        var path = Path.Combine(
            RepoRoot().FullName,
            AcceptanceDocument.Replace('/', Path.DirectorySeparatorChar));

        return File.ReadAllText(path);
    }

    private static DirectoryInfo RepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Dobby.sln")))
        {
            current = current.Parent;
        }

        return current ?? throw new DirectoryNotFoundException("Could not locate Dobby.sln");
    }
}
