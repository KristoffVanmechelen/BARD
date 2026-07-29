using UglyToad.PdfPig.Content;

namespace BARD.Infrastructure.DocumentProcessing;

/// <summary>
/// Groups a PDF page's words into row/column table structure purely
/// from word bounding-box positions (no ruling lines required).
///
/// The Python prototype relied on pdfplumber's extract_tables(), which
/// primarily detects gridded/bordered tables. PdfPig has no equivalent
/// built-in, so this reconstructs the same INPUT SHAPE (a list of rows,
/// each a list of cell strings) that
/// core/ingestion/invoice_parser.py::_extract_product_lines_from_tables
/// (ported to InvoiceParsingService) expects, by clustering words with
/// similar Y-coordinates into rows and then similar X-gaps into columns.
/// This is a reasonable behavioural substitute, not a byte-for-byte port
/// — flagged here because it is the one component of the ingestion
/// pipeline that could not be ported 1:1 given the available .NET
/// libraries (see README "Known limitations").
/// </summary>
public static class BorderlessTableExtractor
{
    private const double RowYTolerance = 3.0;   // points; words within this Y-delta are the same row
    private const double ColumnGapThreshold = 12.0; // points; a gap wider than this starts a new column

    public static IReadOnlyList<IReadOnlyList<string>> ExtractTables(Page page)
    {
        var words = page.GetWords().ToList();
        if (words.Count == 0) return Array.Empty<IReadOnlyList<string>>();

        // Group into rows by Y position (PDF coordinates: origin bottom-left,
        // so sort descending to read top-to-bottom).
        var rows = new List<List<Word>>();
        foreach (var word in words.OrderByDescending(w => w.BoundingBox.Top).ThenBy(w => w.BoundingBox.Left))
        {
            var row = rows.FirstOrDefault(r => Math.Abs(r[0].BoundingBox.Top - word.BoundingBox.Top) <= RowYTolerance);
            if (row is null)
            {
                row = new List<Word>();
                rows.Add(row);
            }
            row.Add(word);
        }

        // A page can contain many non-tabular text rows (headers, addresses,
        // etc.) — only rows with 2+ words are candidate table rows; the
        // downstream numeric-column heuristic (InvoiceParsingService) further
        // filters out non-table rows, matching the Python prototype's
        // tolerance for noisy input.
        var tableRows = new List<IReadOnlyList<string>>();
        foreach (var row in rows.Where(r => r.Count >= 2))
        {
            var orderedWords = row.OrderBy(w => w.BoundingBox.Left).ToList();
            var cells = new List<string>();
            var currentCell = new List<string> { orderedWords[0].Text };
            var lastRight = orderedWords[0].BoundingBox.Right;

            for (int i = 1; i < orderedWords.Count; i++)
            {
                var w = orderedWords[i];
                var gap = w.BoundingBox.Left - lastRight;
                if (gap > ColumnGapThreshold)
                {
                    cells.Add(string.Join(" ", currentCell));
                    currentCell = new List<string>();
                }
                currentCell.Add(w.Text);
                lastRight = w.BoundingBox.Right;
            }
            cells.Add(string.Join(" ", currentCell));

            tableRows.Add(cells);
        }

        return tableRows.Count >= 2 ? tableRows : Array.Empty<IReadOnlyList<string>>();
    }
}
