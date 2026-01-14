using System.Text.RegularExpressions;
using EyeExamParser.DTO;
using EyeExamParser.Helpers;
using Microsoft.Extensions.Logging;

public sealed class ScheduleParser : IScheduleParser
{
    private readonly ILogger<ScheduleParser> _logger;

    public ScheduleParser(ILogger<ScheduleParser> logger)
    {
        _logger = logger;
    }

    public IEnumerable<ScheduleDTO> Parse(IEnumerable<RawScheduleDTO> raw)
    {
        var results = new List<ScheduleDTO>();
        if (raw == null) return results;

        foreach (var item in raw)
        {
            try
            {
                results.Add(ParseSingle(item));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed parsing schedule EntryNumber={EntryNumber}",
                    item?.EntryNumber
                );
            }
        }

        return results;
    }

    /// <summary>
    /// Parses one raw "Schedule of Notices of Leases" entry into a structured DTO.
    /// 
    /// Dataset-driven rules used here (based on observed text extraction patterns):
    /// 
    /// 1) Fixed-width columns:
    ///    - Column boundaries are derived ONCE from the first non-NOTE line (the "template").
    ///    - Template line must contain 4 logical columns separated by 2+ spaces:
    ///        (1) Registration / Plan Ref
    ///        (2) Property Description
    ///        (3) Date of Lease & Term
    ///        (4) Lessee's Title (only on the first line)
    /// 
    /// 2) Aligned slicing is authoritative:
    ///    - If slicing a line by the template boundaries yields any non-empty column values
    ///      AND the line does not look "trimmed", we trust slicing (handles most rows perfectly).
    /// 
    /// 3) Misaligned-but-separated lines:
    ///    - If a line is not aligned but contains 2+ chunks separated by 2+ spaces, we treat it as:
    ///        first chunk  -> Property (col2)
    ///        remaining    -> Lease (col3)
    ///      This matches patterns like: "shop)                         Beginning on"
    /// 
    /// 4) Trimmed single-chunk lines are ambiguous:
    ///    - A line is considered "trimmed" when:
    ///        * raw.Length < templateWidth
    ///        * AND it has no left indentation (starts at index 0)
    ///    - For these lines, the text extractor lost fixed-width alignment, so we route by "right indentation":
    ///        * If the raw line has trailing spaces ("right padding"), it originated from the lease column -> col3
    ///        * Otherwise it belongs to registration continuation -> col1
    /// 
    /// 5) One-line lease carryover after right-indented trimmed lease:
    ///    - Sometimes the extractor removes trailing spaces from the final lease continuation line.
    ///      Example:
    ///        "including 19               "  (has right padding -> col3)
    ///        "April 2028"                  (may lose padding, still belongs to col3)
    ///    - If we routed a trimmed line to col3 because it had right padding, we allow exactly ONE subsequent
    ///      trimmed line (with no right padding) to continue col3, then reset.
    /// 
    /// 6) NOTE lines are isolated:
    ///    - Any line starting with NOTE is excluded from column parsing and stored verbatim in dto.Notes.
    /// 
    /// 7) Output normalization:
    ///    - Parts are joined with single spaces; repeated whitespace collapses; final trim.
    /// </summary>
    private ScheduleDTO ParseSingle(RawScheduleDTO item)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));

        var dto = new ScheduleDTO
        {
            EntryNumber = int.Parse(item.EntryNumber),
            EntryDate = string.IsNullOrWhiteSpace(item.EntryDate) ? null : DateTime.Parse(item.EntryDate)
        };

        var lines = item.EntryText ?? new List<string>();

        // RULE 6: NOTE lines are not part of the 3-column text body; store separately.
        dto.Notes = lines
            .Where(l => l.TrimStart().StartsWith("NOTE", StringComparison.OrdinalIgnoreCase))
            .Select(l => l.Trim())
            .ToList();

        var bodyLines = lines
            .Where(l => !l.TrimStart().StartsWith("NOTE", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (bodyLines.Count == 0)
        {
            if (dto.Notes?.Count == 0) dto.Notes = null;
            return dto;
        }

        // RULE 1: Derive fixed-width column boundaries from the first non-NOTE line (template).
        var template = bodyLines[0] ?? string.Empty;
        int templateWidth = template.Length;

        var cols = Regex.Split(template.TrimEnd(), @"\s{2,}");
        if (cols.Length < 4)
        {
            _logger.LogWarning("First line does not have 4 columns. Entry={EntryNumber}. Line='{Line}'",
                dto.EntryNumber, template);

            if (dto.Notes?.Count == 0) dto.Notes = null;
            return dto;
        }

        // Locate each column start based on the actual template content.
        int col1Start = template.IndexOf(cols[0], StringComparison.Ordinal);
        int col2Start = template.IndexOf(cols[1], col1Start + cols[0].Length, StringComparison.Ordinal);
        int col3Start = template.IndexOf(cols[2], col2Start + cols[1].Length, StringComparison.Ordinal);
        int titleStart = template.IndexOf(cols[3], col3Start + cols[2].Length, StringComparison.Ordinal);

        if (col1Start < 0 || col2Start < 0 || col3Start < 0 || titleStart < 0)
        {
            _logger.LogWarning("Unable to compute boundaries. Entry={EntryNumber}. Line='{Line}'",
                dto.EntryNumber, template);

            if (dto.Notes?.Count == 0) dto.Notes = null;
            return dto;
        }

        int col1End = col2Start;
        int col2End = col3Start;
        int col3End = titleStart;

        // 4th "column" is lessee title, present on first line only.
        dto.LesseesTitle = cols[3].Trim();

        var regParts = new List<string>();
        var propParts = new List<string>();
        var leaseParts = new List<string>();

        string PadRight(string s) => (s ?? string.Empty).PadRight(templateWidth);

        // Indentation logic used only when alignment is missing.
        int ClosestColByIndent(int firstNonSpace)
        {
            int d1 = Math.Abs(firstNonSpace - col1Start);
            int d2 = Math.Abs(firstNonSpace - col2Start);
            int d3 = Math.Abs(firstNonSpace - col3Start);

            if (d1 <= d2 && d1 <= d3) return 1;
            if (d2 <= d3) return 2;
            return 3;
        }

        void AddToCol(int col, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            text = text.Trim();

            if (col == 1) regParts.Add(text);
            else if (col == 2) propParts.Add(text);
            else leaseParts.Add(text);
        }

        // RULE 5: only continue lease (col3) across trimmed/no-indent lines if we "entered" lease
        // via a trimmed line WITH right padding.
        bool prevTrimmedWasCol3BecauseRightIndent = false;

        for (int i = 0; i < bodyLines.Count; i++)
        {
            var raw = bodyLines[i] ?? string.Empty;

            // left indentation measurement (first non-space index)
            int firstNonSpace = 0;
            while (firstNonSpace < raw.Length && raw[firstNonSpace] == ' ')
                firstNonSpace++;

            bool noLeftIndent = firstNonSpace == 0;

            // RULE 4: "trimmed" means the extractor likely lost fixed-width alignment for this line.
            bool looksTrimmed = raw.Length < templateWidth && noLeftIndent;

            // For slicing, pad to template width so substring boundaries are safe.
            var row = PadRight(raw);

            // RULE 2: attempt fixed-width slicing.
            var c1 = SafeSlice(row, col1Start, col1End - col1Start);
            var c2 = SafeSlice(row, col2Start, col2End - col2Start);
            var c3 = SafeSlice(row, col3Start, col3End - col3Start);

            bool anyAligned = !string.IsNullOrWhiteSpace(c1) ||
                              !string.IsNullOrWhiteSpace(c2) ||
                              !string.IsNullOrWhiteSpace(c3);

            // RULE 2: trust slicing when alignment still exists (not trimmed).
            if (anyAligned && !looksTrimmed)
            {
                AddToCol(1, c1);
                AddToCol(2, c2);
                AddToCol(3, c3);

                // aligned rows do not participate in the special "trimmed lease carryover" rule
                prevTrimmedWasCol3BecauseRightIndent = false;
                continue;
            }

            // RULE 3: if a misaligned line still contains clear separation by large gaps, map chunks.
            var trimmedEnd = raw.TrimEnd();
            var chunks = Regex.Split(trimmedEnd, @"\s{2,}")
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            if (chunks.Count >= 2)
            {
                // First chunk continues property (col2), remaining chunk(s) continue lease (col3).
                AddToCol(2, chunks[0]);
                AddToCol(3, string.Join(" ", chunks.Skip(1)));

                // This is not the special "trimmed lease right-padding" case; reset carryover flag.
                prevTrimmedWasCol3BecauseRightIndent = false;
                continue;
            }

            // RULE 4/5: single-chunk fallback when alignment and chunk splitting do not apply.
            var text = raw.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                prevTrimmedWasCol3BecauseRightIndent = false;
                continue;
            }

            // "Right indentation" = trailing spaces in the raw string.
            // Presence of right padding indicates the extractor kept fixed-width column padding.
            int trailingSpaces = raw.Length - raw.TrimEnd().Length;
            bool hasRightIndent = trailingSpaces >= 2; 

            int dest;

            if (looksTrimmed && noLeftIndent)
            {
                // RULE 4: trimmed + no-left-indent -> choose between col1 vs col3 using right padding.
                if (hasRightIndent)
                {
                    // belongs to lease (col3)
                    dest = 3;
                    prevTrimmedWasCol3BecauseRightIndent = true; // enable one-line lease continuation
                }
                else if (prevTrimmedWasCol3BecauseRightIndent)
                {
                    // RULE 5: allow exactly one follow-up trimmed line to continue lease (col3)
                    // Example: "April 2028" may arrive without trailing padding.
                    dest = 3;
                    prevTrimmedWasCol3BecauseRightIndent = false;
                }
                else
                {
                    // belongs to registration (col1)
                    dest = 1;
                    prevTrimmedWasCol3BecauseRightIndent = false;
                }
            }
            else
            {
                // Non-trimmed or indented single-chunk lines: fall back to closest column by indentation.
                dest = ClosestColByIndent(firstNonSpace);
                prevTrimmedWasCol3BecauseRightIndent = false;
            }

            AddToCol(dest, text);
        }

        // RULE 7: normalize output text (join parts, collapse whitespace).
        dto.RegistrationDateAndPlanRef = JsonHelper.Normalize(regParts);
        dto.PropertyDescription = JsonHelper.Normalize(propParts);
        dto.DateOfLeaseAndTerm = JsonHelper.Normalize(leaseParts);

        if (dto.Notes?.Count == 0)
            dto.Notes = null;

        return dto;
    }

    private string SafeSlice(string line, int start, int length)
    {
        if (string.IsNullOrWhiteSpace(line) || start >= line.Length)
            return string.Empty;

        length = Math.Min(length, line.Length - start);
        return line.Substring(start, length).Trim();
    }
}
