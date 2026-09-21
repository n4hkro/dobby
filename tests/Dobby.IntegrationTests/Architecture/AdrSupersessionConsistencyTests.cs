using System.Text.RegularExpressions;
using Shouldly;

namespace Dobby.IntegrationTests.Architecture;

/// <summary>
/// Documentation-shape guard for the `docs/adr/` supersession graph.
/// </summary>
/// <remarks>
/// NFR-027 / ADR-0061 boundary: every assertion below scans ADR header text, so it enforces
/// only the *shape* of the decision record -- that a retired decision names its successor in
/// the field a reader scans first, that the successor claims it back, and that the
/// `README.md` index agrees. None of it is evidence that any decision is correctly
/// implemented, and it closes no acceptance gate.
///
/// The guard exists because two ADRs (`ADR-0026`, `ADR-0035`) carried a bare
/// `**Status:** Superseded` with the successor named only on the following `**Superseded
/// by:**` line. `docs/adr/README.md` § `Update Rule` requires that a changed decision produce
/// a successor ADR and mark the old document `Superseded`, and § `Supersession Index` exists
/// so a reader "hansı sənədin indi qərar sahibi olduğunu axtarmadan bilsin" -- know the
/// current authority without searching. A status-first scan of a bare `Superseded` reports
/// no successor, which is indistinguishable from a genuinely broken supersession chain: the
/// exact failure the index was built to prevent. Two of five retired ADRs were in that state,
/// and the same audit found the reverse direction missing on `ADR-0058` and `ADR-0060`, which
/// declared their supersessions only in prose under `## Consequences`.
/// </remarks>
public sealed class AdrSupersessionConsistencyTests
{
    [Fact]
    public void Every_superseded_adr_names_its_successor_in_the_status_line()
    {
        var violations = AdrFiles()
            .Select(file => (Id: AdrId(file), Status: StatusLine(file)))
            .Where(adr => adr.Status.StartsWith("Superseded", StringComparison.Ordinal))
            .Where(adr => !Regex.IsMatch(adr.Status, @"Superseded by\s+`?ADR-\d{4}`?"))
            .Select(adr => $"{adr.Id}: **Status:** {adr.Status}")
            .ToArray();

        violations.ShouldBeEmpty(
            "a retired ADR must name its successor in the `**Status:**` line itself, not only " +
            "on a following `**Superseded by:**` line: a status-first reader or scan sees a " +
            "bare `Superseded` as a chain with no current authority, which is exactly what " +
            "`docs/adr/README.md` § `Supersession Index` exists to prevent");
    }

    [Fact]
    public void Every_named_successor_exists_and_is_not_itself_a_dead_end()
    {
        var byId = AdrFiles().ToDictionary(AdrId, StatusLine, StringComparer.Ordinal);

        foreach (var (id, status) in byId.Where(entry =>
                     entry.Value.StartsWith("Superseded", StringComparison.Ordinal)))
        {
            var successor = Regex.Match(status, @"Superseded by\s+`?(ADR-\d{4})`?").Groups[1].Value;

            byId.ContainsKey(successor).ShouldBeTrue(
                $"{id} is superseded by {successor}, but no `docs/adr/{successor}-*.md` exists; " +
                "a retired decision pointing at a missing file leaves no reachable authority");

            // A successor that is itself superseded must disclose the onward hop in its own
            // predecessor's status line, so one read resolves the whole chain rather than
            // forcing the reader to walk it file by file.
            if (byId[successor].StartsWith("Superseded", StringComparison.Ordinal))
            {
                status.Contains("chain:", StringComparison.Ordinal).ShouldBeTrue(
                    $"{id} points at {successor}, which is itself superseded; the status line " +
                    "must disclose the onward hop (`chain: ...`) so the current authority is " +
                    "reachable in a single read");
            }
        }
    }

    [Fact]
    public void Every_full_supersession_is_claimed_by_the_successor_it_names()
    {
        var files = AdrFiles().ToArray();

        // Keyed by ADR id, so the check below is an *edge* test. An earlier version of this
        // test flattened every `**Supersedes:**` line in the directory into one set and
        // asserted only that the retired id appeared somewhere in it. That passes when the
        // named successor disclaims the edge and an unrelated ADR happens to claim the id --
        // a misdirected edge is invisible, and only a total orphan fails. The pair must be
        // checked, not set membership.
        var supersedes = files.ToDictionary(
            AdrId,
            file => Regex.Matches(SupersedesLine(file), @"ADR-\d{4}")
                .Select(match => match.Value)
                .ToHashSet(StringComparer.Ordinal),
            StringComparer.Ordinal);

        foreach (var file in files)
        {
            var status = StatusLine(file);
            if (!status.StartsWith("Superseded", StringComparison.Ordinal))
            {
                continue;
            }

            var id = AdrId(file);
            var successor = Regex.Match(status, @"Superseded by\s+`?(ADR-\d{4})`?").Groups[1].Value;
            successor.ShouldNotBeNullOrEmpty(
                $"{id} is superseded but its `**Status:**` line names no successor");

            supersedes.ContainsKey(successor).ShouldBeTrue(
                $"{id} names {successor} as its successor, but that ADR file does not exist");

            supersedes[successor].ShouldContain(
                id,
                $"{id} declares itself superseded by {successor}, so {successor} must claim " +
                $"{id} in its own `**Supersedes:**` line. A claim made by any other ADR, or " +
                "only in `## Consequences` prose, leaves the edge one-directional: the " +
                "successor reads as an unrelated new decision.");
        }
    }

    [Fact]
    public void Partial_amendment_is_declared_as_amends_not_as_supersedes()
    {
        // `**Supersedes:**` is consumed as a full-supersession claim by the test above and by
        // a header-scanning reader. Naming a still-`Accepted` ADR there says "this document
        // is retired" about a document that is not, and registers it as claimed-superseded
        // for free. Partial relations belong in `**Amends:**`.
        var byId = AdrFiles().ToDictionary(AdrId, StatusLine, StringComparer.Ordinal);

        foreach (var file in AdrFiles())
        {
            foreach (Match match in Regex.Matches(SupersedesLine(file), @"ADR-\d{4}"))
            {
                var claimed = match.Value;
                if (!byId.TryGetValue(claimed, out var claimedStatus))
                {
                    continue;
                }

                claimedStatus.StartsWith("Superseded", StringComparison.Ordinal).ShouldBeTrue(
                    $"{AdrId(file)} names {claimed} in its `**Supersedes:**` line, but " +
                    $"{claimed} is [{claimedStatus}]. A partially amended ADR that remains " +
                    "the authority for its untouched sections must be declared with " +
                    "`**Amends:**`, so one field does not carry two different relations.");
            }
        }
    }

    [Fact]
    public void Every_clarifies_edge_is_reciprocated_by_the_adr_it_clarifies()
    {
        // `**Clarifies:**` carries normative weight: it says another ADR requires a field or
        // behaviour whose definition lives here. A one-directional edge leaves the requiring
        // ADR with no reachable pointer to the document that defines what it requires, so an
        // editor of that section never learns this ADR constrains it. `**Supersedes:**` is
        // checked in both directions above; this field gets the same treatment.
        var files = AdrFiles().ToArray();
        var clarifiedBy = files.ToDictionary(
            AdrId,
            file => Regex.Matches(HeaderField(file, "Clarified by"), @"ADR-\d{4}")
                .Select(match => match.Value)
                .ToHashSet(StringComparer.Ordinal),
            StringComparer.Ordinal);

        foreach (var file in files)
        {
            var id = AdrId(file);
            foreach (Match match in Regex.Matches(HeaderField(file, "Clarifies"), @"ADR-\d{4}"))
            {
                var clarified = match.Value;
                clarifiedBy.ContainsKey(clarified).ShouldBeTrue(
                    $"{id} clarifies {clarified}, but no such ADR file exists");

                clarifiedBy[clarified].ShouldContain(
                    id,
                    $"{id} declares `**Clarifies:** {clarified}`, so {clarified} must carry " +
                    $"`**Clarified by:** {id}` in its own header. Otherwise the ADR that " +
                    "requires the clarified behaviour has no header-level pointer to the " +
                    "document defining it, and an edit there cannot know this ADR exists.");
            }
        }
    }

    [Fact]
    public void Every_amended_adr_is_still_accepted_and_names_its_amender()
    {
        // The mirror of the supersession pair-check, for partial relations. An amended ADR
        // stays the authority for the sections the amendment did not touch, so it must stay
        // `Accepted` -- and its `**Status:**` line must disclose the amendment, because a
        // bare `Accepted` tells a status-first reader the text is unchanged since review.
        var byId = AdrFiles().ToDictionary(AdrId, file => file, StringComparer.Ordinal);

        foreach (var file in AdrFiles())
        {
            var id = AdrId(file);
            foreach (Match match in Regex.Matches(HeaderField(file, "Amends"), @"ADR-\d{4}"))
            {
                var amended = match.Value;
                byId.ContainsKey(amended).ShouldBeTrue(
                    $"{id} amends {amended}, but no such ADR file exists");

                var amendedStatus = StatusLine(byId[amended]);
                amendedStatus.StartsWith("Accepted", StringComparison.Ordinal).ShouldBeTrue(
                    $"{id} amends {amended}, so {amended} must remain `Accepted`: an amended " +
                    "ADR keeps authority over the sections the amendment did not touch. If " +
                    "it is fully retired the relation is `**Supersedes:**`, not `**Amends:**`.");

                amendedStatus.Contains(id, StringComparison.Ordinal).ShouldBeTrue(
                    $"{amended} is amended by {id}, so its `**Status:**` line must name {id}; " +
                    $"the line currently reads [{amendedStatus}]. A bare `Accepted` reports to " +
                    "a status-first reader that the document is unchanged, which is the drift " +
                    "this suite exists to catch.");
            }
        }
    }

    [Fact]
    public void Every_status_declared_amendment_is_claimed_by_the_amender_it_names()
    {
        // The reverse direction of the check above, and the one that was missing. That check
        // walks `**Amends:**` -> amendee `**Status:**`, so it only ever sees relations the
        // AMENDER declares. An amendment recorded solely in the amendee's `**Status:**` line was
        // invisible to the whole suite: eight of the nine relations in `docs/adr/` were in that
        // state at once -- ADR-0011, ADR-0019, ADR-0031, ADR-0032, ADR-0040, ADR-0041, ADR-0055
        // and ADR-0057 all announced "amended by ADR-NNNN" while the named ADR carried no
        // `**Amends:**` header at all, and the suite stayed green over every one of them.
        //
        // The missing direction is the one a reader of the amender needs. `docs/adr/README.md`
        // § `Supersession header formati` requires both, precisely because a document that
        // silently constrains an older decision gives an editor of that older decision no
        // header-level way to find out.
        var amends = AdrFiles().ToDictionary(
            AdrId,
            file => Regex.Matches(HeaderField(file, "Amends"), @"ADR-\d{4}")
                .Select(match => match.Value)
                .ToHashSet(StringComparer.Ordinal),
            StringComparer.Ordinal);

        foreach (var file in AdrFiles())
        {
            var id = AdrId(file);
            var status = StatusLine(file);
            if (!status.Contains("amended", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Only ids introduced by the amendment clause count. A `Status` line may also name
            // the ADR that *clarifies* it (ADR-0040 names both), and a clarification is a
            // different relation carrying `**Clarifies:**`, not `**Amends:**`.
            var amendmentClause = status[status.IndexOf("amended", StringComparison.OrdinalIgnoreCase)..];
            var clarifiedAt = amendmentClause.IndexOf("clarified", StringComparison.OrdinalIgnoreCase);
            if (clarifiedAt >= 0)
            {
                amendmentClause = amendmentClause[..clarifiedAt];
            }

            foreach (Match match in Regex.Matches(amendmentClause, @"ADR-\d{4}"))
            {
                var amender = match.Value;
                amends.ContainsKey(amender).ShouldBeTrue(
                    $"{id} declares itself amended by {amender}, but no such ADR file exists");

                amends[amender].ShouldContain(
                    id,
                    $"{id} declares itself amended by {amender}, so {amender} must claim {id} in " +
                    $"its own `**Amends:**` header. Without it the relation is one-directional: " +
                    $"a reader of {amender} cannot learn it constrains {id}, and an editor of " +
                    $"{id} has no header-level pointer to the document that narrowed it.");
            }
        }
    }

    [Fact]
    public void An_accepted_adr_never_describes_a_partial_relation_with_supersession_vocabulary()
    {
        // `docs/adr/README.md` § `Supersession header formati` defines exactly two relations:
        // full supersession (`**Supersedes:**`, where the named ADR's own `Status` must read
        // `Superseded`) and partial amendment (`**Amends:**`, where the amendee stays
        // `Accepted`). "Accepted (... superseded by ADR-NNNN)" is a third, undefined
        // vocabulary, and it is not harmlessly informal: seven ADRs carried it at once while
        // the README's own Supersession Index classified all seven as `Accepted (partial)` --
        // the Status lines and the index contradicted each other -- and because every other
        // guard in this suite keys the partial relation on the literal word "amended", each
        // of those seven bypassed the reverse-edge and dated-section checks entirely. This
        // test closes that bypass: an `Accepted` document that names another ADR in its
        // status parenthetical must phrase the relation as an amendment, which is what makes
        // the rest of the suite see and verify it.
        //
        // Prose like "license authority is ADR-0060" stays legal: only the supersession words
        // themselves are barred from an Accepted status line.
        foreach (var file in AdrFiles())
        {
            var status = StatusLine(file);
            if (status.StartsWith("Superseded", StringComparison.Ordinal))
            {
                continue;
            }

            Regex.IsMatch(status, @"supersed", RegexOptions.IgnoreCase).ShouldBeFalse(
                $"{AdrId(file)} is Accepted but its `**Status:**` line [{status}] uses " +
                "supersession vocabulary for what must be a partial amendment. " +
                "`docs/adr/README.md` reserves \"superseded\" for full retirement (Status " +
                "`Superseded`); a partial relation is written \"amended by ADR-NNNN\", which " +
                "is also the phrasing the reverse-edge and dated-section guards in this " +
                "suite key on -- the third vocabulary made those relations invisible to " +
                "every check here.");
        }
    }

    [Fact]
    public void An_amender_never_calls_its_own_amendment_a_supersession()
    {
        // The vocabulary guard above reads only the `**Status:**` line, so it sees the amendee
        // side of a relation and nothing else. All four amender ADRs sat in that blind spot at
        // once: `ADR-0058` § `Consequences` said it "supersedes the saved-credential semantics
        // in ADR-0038 and ADR-0055" while its own `**Amends:**` header declared both as
        // amendments; `ADR-0059` said it "supersedes the minimum page/lifecycle portions" of
        // ADRs it amends; `ADR-0060` said it "supersedes the license-as-setup-proof sections of
        // ADR-0057"; `ADR-0061` claimed to "partially supersede" four documents, which is not a
        // relation `docs/adr/README.md` defines at all. A reader who believes the body concludes
        // those ADRs are retired and stops reading them -- but each stays `Accepted` and stays
        // the authority for every section the amendment did not touch, so the reader silently
        // loses the decisions that were never amended.
        //
        // Scoped to `## Consequences`, where an ADR summarises what it does to its neighbours.
        // `## Revision History` is deliberately not scanned: a dated entry has to be able to
        // quote the wording it retired, which is the one place the old vocabulary is correct.
        foreach (var file in AdrFiles())
        {
            var amendees = DeclaredIds(HeaderField(file, "Amends"));
            if (amendees.Count == 0)
            {
                continue;
            }

            var text = File.ReadAllText(file.FullName).Replace("\r\n", "\n", StringComparison.Ordinal);

            foreach (var sentence in Sentences(Section(text, "## Consequences")))
            {
                if (!sentence.Contains("supersed", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var named = amendees
                    .Where(id => sentence.Contains(id, StringComparison.Ordinal))
                    .ToList();

                named.ShouldBeEmpty(
                    $"{AdrId(file)} declares `**Amends:** {string.Join(", ", named)}` in its " +
                    $"header but its `## Consequences` sentence [{sentence}] describes the same " +
                    "relation as a supersession. The two authorities contradict each other, and " +
                    "the body is the one a reader acts on: it says the named ADR is retired " +
                    "while the header says it stays `Accepted` and keeps authority over its " +
                    "untouched sections. Write \"amends\" here; keep \"supersedes\" for a " +
                    "document whose own `**Status:**` reads `Superseded`.");
            }
        }
    }

    [Fact]
    public void Cross_reference_supersession_claims_name_a_retired_adr()
    {
        // Same blind spot, other direction. `ADR-0006` and `ADR-0012` each carried a
        // `## Cross-references` line saying `ADR-0059` "bu ADR-in ... hissalarini supersede
        // edir" -- while both `ADR-0059`'s header and their own `**Status:**` lines called it an
        // amendment. A cross-reference is the line a reader follows to find the current
        // authority, so a supersession claim there redirects them away from a document that is
        // still binding.
        //
        // The rule is deliberately narrow: a cross-reference line may use supersession
        // vocabulary only when it is about a document that really is retired -- either an ADR it
        // names whose `**Status:**` is `Superseded`, or the file itself, which lets a retired ADR
        // point forward to the successor that replaced it. So "`ADR-0039` -- superseded history
        // (`ADR-0058`)" stays legal, and "`ADR-0059` -- supersedes this ADR's claims" does not.
        var statuses = AdrFiles().ToDictionary(AdrId, StatusLine, StringComparer.Ordinal);

        foreach (var file in AdrFiles())
        {
            var text = File.ReadAllText(file.FullName).Replace("\r\n", "\n", StringComparison.Ordinal);

            foreach (var line in Section(text, "## Cross-references").Split('\n'))
            {
                if (!line.Contains("supersed", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var named = DeclaredIds(line);
                if (named.Count == 0)
                {
                    // A generic pointer such as `docs/adr/README.md` § `Supersession header
                    // formati` names no ADR and claims nothing about one.
                    continue;
                }

                var retired = named
                    .Append(AdrId(file))
                    .Any(id => statuses.TryGetValue(id, out var status)
                        && status.StartsWith("Superseded", StringComparison.Ordinal));

                retired.ShouldBeTrue(
                    $"{AdrId(file)} § `Cross-references` line [{line.Trim()}] uses supersession " +
                    $"vocabulary, but neither {AdrId(file)} nor {string.Join("/", named)} has a " +
                    "`**Status:**` of `Superseded`. `docs/adr/README.md` reserves that word for " +
                    "full retirement; a partial relation reads \"amends\"/\"amended by\". As " +
                    "written, the line sends a reader away from a document that is still the " +
                    "authority for everything the amendment left alone.");
            }
        }
    }

    [Fact]
    public void An_accepted_adr_never_marks_a_decision_in_place_as_superseded()
    {
        // `ADR-0057` kept its retired setup-proof requirement -- an existing `.dobbylicense`
        // supplied as a read-only file argument -- word for word in the decision body and
        // appended a "**Superseded by `ADR-0060`:**" note after it. A reader who stopped at the
        // first sentence would have implemented the gate `ADR-0060` removed, which is the
        // strongest failure mode in this directory: an in-body marker leaves two contradictory
        // requirements standing and lets the reader pick.
        //
        // A retired position belongs in `## Revision History` (that section is exempt, and a
        // `Superseded` ADR's own `**Superseded by:**` header field is exempt because the whole
        // document is retired); the decision body carries only the current requirement.
        foreach (var file in AdrFiles())
        {
            if (StatusLine(file).StartsWith("Superseded", StringComparison.Ordinal))
            {
                continue;
            }

            var text = File.ReadAllText(file.FullName).Replace("\r\n", "\n", StringComparison.Ordinal);
            var inRevisionHistory = false;

            foreach (var line in text.Split('\n'))
            {
                if (line.StartsWith("## ", StringComparison.Ordinal))
                {
                    inRevisionHistory = line.Contains("Revision History", StringComparison.OrdinalIgnoreCase);
                }

                if (inRevisionHistory)
                {
                    continue;
                }

                Regex.IsMatch(line, @"\*\*Superseded by", RegexOptions.IgnoreCase).ShouldBeFalse(
                    $"{AdrId(file)} is `Accepted`, so no line in its body may carry a " +
                    $"`**Superseded by ...:**` marker; [{line.Trim()}] does. Such a marker is " +
                    "always bolted onto prose that is still standing, so the retired and the " +
                    "current requirement both read as normative. Move the retired wording into " +
                    "a dated `## Revision History` entry and leave only the current requirement " +
                    "in the decision.");
            }
        }
    }


    [Fact]
    public void Every_amended_adr_carries_a_dated_section_recording_the_amendment()
    {
        // `docs/adr/README.md` requires two conditions *jointly* of a partially amended
        // `Accepted` ADR: the `**Status:**` line records it -- checked above, since "audit ve
        // tooling ilk novbede `Status`-u oxuyur" -- and a dated section says what changed. The
        // status line alone is a claim with no body: it tells a reader an amendment happened
        // without telling them which sentences moved, which is not enough to act on.
        //
        // A `## Revision History` bullet dated `**YYYY-MM-DD:**` satisfies this (the dominant
        // shape in this directory), as does a dated `## ... Amendment` section carrying a
        // `**Tarix:**` or `**Date:**` line. An undated `## ... Amendment` does not: ADR-0019 and
        // ADR-0041 both carry one about an unrelated authority (Branch Shared), so accepting an
        // undated section would let those pass while saying nothing about the amendment the
        // status line actually declares.
        foreach (var file in AdrFiles())
        {
            var status = StatusLine(file);
            if (!status.Contains("amended", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var text = File.ReadAllText(file.FullName).Replace("\r\n", "\n", StringComparison.Ordinal);
            var hasDatedRevisionBullet =
                Regex.IsMatch(
                    Section(text, "## Revision History"),
                    @"^- \*\*\d{4}-\d{2}-\d{2}",
                    // Multiline is required: `Section` returns the body *after* the heading, so it
                    // starts with a newline and a string-anchored `^` can never reach the first
                    // bullet. Without this the check fails on every correctly dated ADR, which
                    // would have made the guard a pure false-positive generator.
                    RegexOptions.Multiline);
            var hasDatedAmendmentSection = Regex.Matches(text, @"^## .*Amendment.*$", RegexOptions.Multiline)
                .Any(heading => Regex.IsMatch(
                    Section(text, heading.Value),
                    @"\*\*(Tarix|Date):\*\*\s*\d{4}-\d{2}-\d{2}"));

            (hasDatedRevisionBullet || hasDatedAmendmentSection).ShouldBeTrue(
                $"{AdrId(file)} declares itself amended in its `**Status:**` line, so it must " +
                "also carry a dated section saying what changed -- a `## Revision History` " +
                "bullet dated `**YYYY-MM-DD:**`, or a `## ... Amendment` section with a dated " +
                "`**Tarix:**`/`**Date:**` line. `docs/adr/README.md` requires both conditions " +
                "together; a status claim with no dated body tells a reader an amendment " +
                "happened but not which sentences it moved.");
        }
    }

    [Fact]
    public void Supersession_index_status_column_matches_each_adr_status_line()
    {
        var readme = File.ReadAllText(Path.Combine(AdrDirectory().FullName, "README.md"));

        foreach (var file in AdrFiles())
        {
            var status = StatusLine(file);
            if (!status.StartsWith("Superseded", StringComparison.Ordinal))
            {
                continue;
            }

            // The index keys retired rows by file name, so the row must carry the same status
            // text the ADR itself declares; a stale column would re-create the ambiguity this
            // suite closes.
            var row = readme
                .Split('\n')
                .FirstOrDefault(line => line.Contains($"`{file.Name}`", StringComparison.Ordinal)
                    && line.StartsWith("|", StringComparison.Ordinal));

            var indexRow = row.ShouldNotBeNull(
                $"{file.Name} is superseded, so `docs/adr/README.md` § `Supersession Index` " +
                "must carry a row naming its current authority");

            indexRow.Contains(status, StringComparison.Ordinal).ShouldBeTrue(
                $"the Supersession Index status column for {file.Name} must repeat that ADR's " +
                $"own `**Status:**` text verbatim; the ADR says [{status}] but the index row " +
                "disagrees, so the two authorities would drift apart silently");
        }
    }

    [Fact]
    public void Numbering_gaps_are_documented_and_no_gap_number_is_silently_reused()
    {
        var readme = File.ReadAllText(Path.Combine(AdrDirectory().FullName, "README.md"));

        // Only the `### Numbering gaps` section may explain a missing number. Searching the
        // whole README also matches the partial-supersession table, where a row like
        // ADR-0038 / Accepted (partial) would let a *deleted* ADR file pass as a documented
        // gap.
        var gapSection = Section(readme, "### Numbering gaps");
        gapSection.ShouldNotBeNullOrWhiteSpace(
            "`docs/adr/README.md` must carry a `### Numbering gaps` section; without it this " +
            "guard would silently degrade to accepting any README mention");

        var present = AdrFiles().Select(AdrId).ToHashSet(StringComparer.Ordinal);
        var highest = present.Select(id => int.Parse(id[4..])).Max();

        // Numbering starts at `ADR-0001`: `ADR-0000` is the template, deliberately excluded
        // from `AdrFiles()`, and is not a decision that could have gone missing.
        for (var number = 1; number <= highest; number++)
        {
            var id = $"ADR-{number:D4}";
            if (present.Contains(id))
            {
                continue;
            }

            // `README.md` § `Numbering gaps` is the only place a missing number is allowed to
            // be explained. An undocumented hole is indistinguishable from a deleted decision.
            gapSection.Contains($"| `{id}` |", StringComparison.Ordinal).ShouldBeTrue(
                $"{id} has no ADR file, so `docs/adr/README.md` § `Numbering gaps` must carry " +
                "a row explaining where that decision now lives; an unexplained hole reads as " +
                "a decision that was deleted rather than folded into another ADR");
        }
    }

    private static IReadOnlyList<string> DeclaredIds(string text) =>
        Regex.Matches(text, @"ADR-\d{4}")
            .Select(match => match.Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    // Sentence split for relation prose. Whitespace is collapsed first so a claim that wraps
    // across continuation lines is still read as one sentence; `;` and `:` end a clause here
    // because relation summaries in this directory routinely join two relations with them.
    private static IEnumerable<string> Sentences(string markdown) =>
        Regex.Split(Regex.Replace(markdown, @"\s+", " "), @"(?<=[.;:])\s+")
            .Where(sentence => !string.IsNullOrWhiteSpace(sentence));


    // The text of one section, up to the next heading of the same or higher level.
    private static string Section(string markdown, string heading)
    {
        var text = markdown.Replace("\r\n", "\n", StringComparison.Ordinal);
        var start = text.IndexOf(heading + "\n", StringComparison.Ordinal);
        if (start < 0)
        {
            return string.Empty;
        }

        var body = text[(start + heading.Length)..];
        var next = Regex.Match(body, @"^#{1,3} ", RegexOptions.Multiline);
        return next.Success ? body[..next.Index] : body;
    }

    private static string AdrId(FileInfo file) => file.Name[..8];

    private static string StatusLine(FileInfo file) => HeaderField(file, "Status");

    private static string SupersedesLine(FileInfo file) => HeaderField(file, "Supersedes");

    private static string HeaderField(FileInfo file, string field)
    {
        // Header fields may wrap across continuation lines, so capture up to the next blank
        // line or the next `**Field:**` marker.
        var text = File.ReadAllText(file.FullName).Replace("\r\n", "\n", StringComparison.Ordinal);
        var match = Regex.Match(
            text,
            $@"^\*\*{field}:\*\*[ ]*(?<value>.*?)(?=\n[ ]*\n|\n\*\*|\n##)",
            RegexOptions.Multiline | RegexOptions.Singleline);

        return match.Success
            ? Regex.Replace(match.Groups["value"].Value, @"\s+", " ").Trim()
            : string.Empty;
    }

    private static IEnumerable<FileInfo> AdrFiles() =>
        AdrDirectory()
            .EnumerateFiles("ADR-*.md")
            .Where(file => !file.Name.StartsWith("ADR-0000", StringComparison.Ordinal))
            .OrderBy(file => file.Name, StringComparer.Ordinal);

    private static DirectoryInfo AdrDirectory() =>
        new(Path.Combine(RepoRoot().FullName, "docs", "adr"));

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
