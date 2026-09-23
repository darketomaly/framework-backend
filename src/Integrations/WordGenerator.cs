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
                    .preview-block { margin-top: 24px; }
                    .preview-block h2 { font-size: 14px; font-weight: 500; margin: 0 0 8px; }
                    .preview { background: #050708; border-radius: 8px; display: block; max-width: 100%; }
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
                    <div class="preview-block" id="original-block" hidden>
                        <h2>Original</h2>
                        <img class="preview" id="original-preview" alt="Original generated word" >
                    </div>
                    <div class="preview-block" id="discord-block" hidden>
                        <h2>Discord ready</h2>
                        <img class="preview" id="discord-preview" alt="Discord-ready generated word">
                    </div>
                </main>
                <script>
                    const form = document.getElementById("form");
                    const input = document.getElementById("word");
                    const status = document.getElementById("status");
                    const originalBlock = document.getElementById("original-block");
                    const discordBlock = document.getElementById("discord-block");
                    const originalPreview = document.getElementById("original-preview");
                    const discordPreview = document.getElementById("discord-preview");
                    let previousUrls = [];

                    form.addEventListener("submit", async (event) => {
                        event.preventDefault();
                        const word = input.value.trim();
                        if (!/^[A-Za-z]+$/.test(word) || word.length > 100) {
                            status.textContent = "Enter a word containing only letters.";
                            originalBlock.hidden = true;
                            discordBlock.hidden = true;
                            return;
                        }

                        status.textContent = "Generating...";
                        try {
                            const [originalResponse, discordResponse] = await Promise.all([
                                fetch(`/generate-word/${encodeURIComponent(word)}`),
                                fetch(`/generate-word/${encodeURIComponent(word)}?format=discord`)
                            ]);
                            if (!originalResponse.ok || !discordResponse.ok) {
                                throw new Error("The word could not be generated.");
                            }

                            previousUrls.forEach(url => URL.revokeObjectURL(url));
                            const originalUrl = URL.createObjectURL(await originalResponse.blob());
                            const discordUrl = URL.createObjectURL(await discordResponse.blob());
                            previousUrls = [originalUrl, discordUrl];
                            originalPreview.src = originalUrl;
                            discordPreview.src = discordUrl;
                            originalBlock.hidden = false;
                            discordBlock.hidden = false;
                            status.textContent = "";
                        } catch (error) {
                            status.textContent = error.message;
                            originalBlock.hidden = true;
                            discordBlock.hidden = true;
                        }
                    });
                </script>
            </body>
            </html>
            """,
            "text/html"));

        app.MapGet("/generate-word/{word}", (string word, HttpContext context) =>
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
                var discordReady = context.Request.Query["format"] == "discord";
                return Results.File(
                    Generate(word, discordReady),
                    "image/png");
            }
            catch (FileNotFoundException)
            {
                return Results.Problem("The sprite sheet is not available.", statusCode: 503);
            }
        });
    }

    private static byte[] Generate(string word, bool discordReady)
    {
        var spritePath = Path.Combine(AppContext.BaseDirectory, "Sprites", SpriteFileName);
        using var sheet = Image.Load<Rgba32>(spritePath);
        var letters = new Dictionary<char, Image<Rgba32>>(26);

        var letterIndex = 0;
        var glyphRows = FindGlyphRows(sheet);
        foreach (var (rowTop, rowBottom) in glyphRows)
        {
            var glyphRuns = FindGlyphRuns(sheet, rowTop, rowBottom + 1);

            foreach (var (left, right) in glyphRuns)
            {
                var (glyphTop, glyphBottom) = FindGlyphVerticalBounds(
                    sheet,
                    left,
                    right,
                    rowTop,
                    rowBottom + 1);
                var cell = sheet.Clone(context => context.Crop(new Rectangle(
                    left,
                    glyphTop,
                    right - left + 1,
                    glyphBottom - glyphTop + 1)));

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
            var canvasWidth = discordReady ? 1370 : width;
            var canvasHeight = discordReady ? 266 : height;
            using var result = new Image<Rgba32>(canvasWidth, canvasHeight, Color.Transparent);

            var x = 0;
            foreach (var letter in requestedLetters)
            {
                var y = discordReady
                    ? (canvasHeight - letter.Height) / 2
                    : canvasHeight - letter.Height;
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

    private static List<(int Top, int Bottom)> FindGlyphRows(Image<Rgba32> sheet)
    {
        var rows = new List<(int Top, int Bottom)>();
        var inRun = false;
        var top = 0;

        sheet.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < sheet.Height; y++)
            {
                var hasPixels = false;
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    if (row[x].A != 0)
                    {
                        hasPixels = true;
                        break;
                    }
                }

                if (hasPixels && !inRun)
                {
                    top = y;
                    inRun = true;
                }
                else if (!hasPixels && inRun)
                {
                    rows.Add((top, y - 1));
                    inRun = false;
                }
            }
        });

        if (inRun)
        {
            rows.Add((top, sheet.Height - 1));
        }

        if (rows.Count != GridRows)
        {
            throw new InvalidOperationException("The sprite sheet does not contain four letter rows.");
        }

        return rows;
    }

    private static (int Top, int Bottom) FindGlyphVerticalBounds(
        Image<Rgba32> sheet,
        int left,
        int right,
        int top,
        int bottom)
    {
        var rowPixelCounts = new int[bottom - top];

        sheet.ProcessPixelRows(accessor =>
        {
            for (var y = top; y < bottom; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = left; x <= right; x++)
                {
                    if (row[x].A != 0)
                    {
                        rowPixelCounts[y - top]++;
                    }
                }
            }
        });

        var bestTop = -1;
        var bestBottom = -1;
        var currentTop = -1;

        for (var index = 0; index < rowPixelCounts.Length; index++)
        {
            if (rowPixelCounts[index] > 0 && currentTop < 0)
            {
                currentTop = index;
            }

            var isLastRow = index == rowPixelCounts.Length - 1;
            if (currentTop >= 0 && (rowPixelCounts[index] == 0 || isLastRow))
            {
                var currentBottom = rowPixelCounts[index] == 0 ? index - 1 : index;
                if (bestTop < 0 ||
                    currentBottom - currentTop > bestBottom - bestTop)
                {
                    bestTop = currentTop;
                    bestBottom = currentBottom;
                }

                currentTop = -1;
            }
        }

        if (bestTop < 0)
        {
            throw new InvalidOperationException("A sprite cell is empty.");
        }

        return (top + bestTop, top + bestBottom);
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
