using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace framework_backend;

public static class WordGenerator
{
    private const int GridRows = 4;
    private const int LetterSpacing = 8;
    private const int MaxWordLength = 100;
    private const string SpriteFileName = "T55ht (1).png";

    public static void Configure(WebApplication app)
    {
        app.MapGet("/generate-word", () => Results.Content(
            """
            <!doctype html>
            <html lang="en">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>Generate word</title>
                <style>
                    :root { color-scheme: dark; }
                    body { background: #101416; color: #eaf0f2; font: 16px system-ui, sans-serif; margin: 0; padding: 32px; }
                    main { max-width: 720px; margin: auto; }
                    form { display: flex; gap: 8px; }
                    input, button { border: 1px solid #53616a; border-radius: 6px; font: inherit; padding: 10px 12px; }
                    input { background: #050708; color: inherit; flex: 1; }
                    button { background: #eaf0f2; color: #101416; cursor: pointer; }
                    #status { color: #aebbc1; min-height: 24px; }
                    #preview { background: #050708; border-radius: 8px; display: block; max-width: 100%; }
                </style>
            </head>
            <body>
                <main>
                    <h1>Generate word</h1>
                    <form id="form">
                        <input id="word" name="word" maxlength="100" pattern="[A-Za-z]+" required autofocus>
                        <button type="submit">Generate</button>
                    </form>
                    <p id="status"></p>
                    <img id="preview" alt="Generated word preview" hidden>
                </main>
                <script>
                    const form = document.getElementById("form");
                    const input = document.getElementById("word");
                    const status = document.getElementById("status");
                    const preview = document.getElementById("preview");
                    let previousUrl;

                    form.addEventListener("submit", async (event) => {
                        event.preventDefault();
                        const word = input.value.trim();
                        if (!/^[A-Za-z]+$/.test(word) || word.length > 100) {
                            status.textContent = "Enter a word containing only letters.";
                            preview.hidden = true;
                            return;
                        }

                        status.textContent = "Generating...";
                        try {
                            const response = await fetch(`/generate-word/${encodeURIComponent(word)}`);
                            if (!response.ok) {
                                throw new Error("The word could not be generated.");
                            }

                            const blob = await response.blob();
                            if (previousUrl) {
                                URL.revokeObjectURL(previousUrl);
                            }
                            previousUrl = URL.createObjectURL(blob);
                            preview.src = previousUrl;
                            preview.hidden = false;
                            status.textContent = "";
                        } catch (error) {
                            status.textContent = error.message;
                            preview.hidden = true;
                        }
                    });
                </script>
            </body>
            </html>
            """,
            "text/html"));

        app.MapGet("/generate-word/{word}", (string word) =>
        {
            if (string.IsNullOrWhiteSpace(word) ||
                word.Length > MaxWordLength ||
                word.Any(character =>
                    (character < 'a' || character > 'z') &&
                    (character < 'A' || character > 'Z')))
            {
                return Results.BadRequest(new
                {
                    error = $"The word must contain only letters and be at most {MaxWordLength} characters long."
                });
            }

            try
            {
                return Results.File(
                    Generate(word),
                    "image/png");
            }
            catch (FileNotFoundException)
            {
                return Results.Problem("The sprite sheet is not available.", statusCode: 503);
            }
        });
    }

    private static byte[] Generate(string word)
    {
        var spritePath = Path.Combine(AppContext.BaseDirectory, "Sprites", SpriteFileName);
        using var sheet = Image.Load<Rgba32>(spritePath);
        var letters = new Dictionary<char, Image<Rgba32>>(26);

        var letterIndex = 0;
        var rowHeight = sheet.Height / GridRows;
        for (var row = 0; row < GridRows; row++)
        {
            var rowTop = row * rowHeight;
            var rowBottom = row == GridRows - 1 ? sheet.Height : rowTop + rowHeight;
            var glyphRuns = FindGlyphRuns(sheet, rowTop, rowBottom);

            foreach (var (left, right) in glyphRuns)
            {
                var cell = sheet.Clone(context => context.Crop(new Rectangle(
                    left,
                    rowTop,
                    right - left + 1,
                    rowBottom - rowTop)));

                letters[(char)('A' + letterIndex)] = TrimTransparentMargins(cell);
                letterIndex++;
            }
        }

        if (letterIndex != 26)
        {
            throw new InvalidOperationException("The sprite sheet does not contain 26 letters.");
        }

        try
        {
            var requestedLetters = word.ToUpperInvariant()
                .Select(character => letters[character])
                .ToArray();
            var width = requestedLetters.Sum(letter => letter.Width) +
                        LetterSpacing * (requestedLetters.Length - 1);
            var height = requestedLetters.Max(letter => letter.Height);
            using var result = new Image<Rgba32>(width, height, Color.Transparent);

            var x = 0;
            foreach (var letter in requestedLetters)
            {
                var y = height - letter.Height;
                result.Mutate(context => context.DrawImage(letter, new Point(x, y), 1f));
                x += letter.Width + LetterSpacing;
            }

            using var output = new MemoryStream();
            result.Save(output, new PngEncoder());
            return output.ToArray();
        }
        finally
        {
            foreach (var letter in letters.Values)
            {
                letter.Dispose();
            }
        }
    }

    private static List<(int Left, int Right)> FindGlyphRuns(
        Image<Rgba32> sheet,
        int top,
        int bottom)
    {
        var runs = new List<(int Left, int Right)>();
        var columnHasPixels = new bool[sheet.Width];
        var inRun = false;
        var left = 0;

        sheet.ProcessPixelRows(accessor =>
        {
            for (var y = top; y < bottom; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    if (row[x].A != 0)
                    {
                        columnHasPixels[x] = true;
                    }
                }
            }
        });

        for (var x = 0; x < columnHasPixels.Length; x++)
        {
            if (columnHasPixels[x] && !inRun)
            {
                left = x;
                inRun = true;
            }
            else if (!columnHasPixels[x] && inRun)
            {
                runs.Add((left, x - 1));
                inRun = false;
            }
        }

        if (inRun)
        {
            runs.Add((left, sheet.Width - 1));
        }

        return runs;
    }

    private static Image<Rgba32> TrimTransparentMargins(Image<Rgba32> image)
    {
        var bounds = new Rectangle(image.Width, image.Height, 0, 0);

        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < image.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    if (row[x].A == 0)
                    {
                        continue;
                    }

                    bounds = bounds.Width == 0
                        ? new Rectangle(x, y, 1, 1)
                        : Rectangle.Union(bounds, new Rectangle(x, y, 1, 1));
                }
            }
        });

        if (bounds.Width == 0 || bounds.Height == 0)
        {
            throw new InvalidOperationException("A sprite cell is empty.");
        }

        var trimmed = image.Clone(context => context.Crop(bounds));
        image.Dispose();
        return trimmed;
    }
}
