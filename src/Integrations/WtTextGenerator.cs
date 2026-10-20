using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace framework_backend;

public static class WtTextGenerator
{
    private const int GridRows = 6;
    private const int ExpectedGlyphCount = 36;
    private const int MaxTextLength = 100;
    private const int DiscordWidth = 1370;
    private const int DiscordHeight = 266;
    private const int LetterSpacing = 12;
    private const int Padding = 12;
    private const string SpriteFileName = "wt-spritesheet.png";
    private const string GlyphOrder = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public static void Configure(WebApplication app)
    {
        app.MapGet("/generate-wt", () => Results.Content(
            """
            <!doctype html>
            <html lang="en">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>Generate WT text</title>
                <style>
                    :root { color-scheme: dark; }
                    body { background: #101416; color: #eaf0f2; font: 16px system-ui, sans-serif; margin: 0; padding: 32px; }
                    main { max-width: 900px; margin: auto; }
                    form { display: flex; gap: 8px; }
                    input, button { border: 1px solid #53616a; border-radius: 6px; font: inherit; padding: 10px 12px; }
                    input { background: #050708; color: inherit; flex: 1; }
                    button { background: #eaf0f2; color: #101416; cursor: pointer; }
                    #status { color: #aebbc1; min-height: 24px; }
                    .preview-block { margin-top: 24px; }
                    .preview-block h2 { font-size: 14px; font-weight: 500; margin: 0 0 8px; }
                    .preview { background-color: #050708; background-image: linear-gradient(45deg, #161d20 25%, transparent 25%), linear-gradient(-45deg, #161d20 25%, transparent 25%), linear-gradient(45deg, transparent 75%, #161d20 75%), linear-gradient(-45deg, transparent 75%, #161d20 75%); background-size: 20px 20px; background-position: 0 0, 0 10px, 10px -10px, -10px 0; border-radius: 8px; display: block; max-width: 100%; }
                    .download { display: inline-block; margin-top: 8px; }
                </style>
            </head>
            <body>
                <main>
                    <h1>Generate WT text</h1>
                    <form id="form">
                        <input id="text" name="text" maxlength="100" pattern="[A-Za-z0-9 ]+" required autofocus aria-label="Text">
                        <button type="submit">Generate</button>
                    </form>
                    <p id="status"></p>
                    <div class="preview-block" id="preview-block" hidden>
                        <h2>Discord ready</h2>
                        <img class="preview" id="preview" alt="Discord-ready generated text">
                        <button class="download" id="download" type="button">Download PNG</button>
                    </div>
                </main>
                <script>
                    const form = document.getElementById("form");
                    const input = document.getElementById("text");
                    const status = document.getElementById("status");
                    const previewBlock = document.getElementById("preview-block");
                    const preview = document.getElementById("preview");
                    const download = document.getElementById("download");
                    let imageUrl = "";
                    let currentText = "";

                    download.addEventListener("click", () => {
                        const link = document.createElement("a");
                        link.href = imageUrl;
                        link.download = `${currentText || "text"}-discord.png`;
                        link.click();
                    });

                    form.addEventListener("submit", async (event) => {
                        event.preventDefault();
                        const text = input.value.trim();
                        if (!/^[A-Za-z0-9 ]+$/.test(text) || text.length > 100) {
                            status.textContent = "Use letters, numbers, and spaces only (up to 100 characters).";
                            previewBlock.hidden = true;
                            return;
                        }

                        status.textContent = "Generating...";
                        previewBlock.hidden = true;
                        try {
                            const response = await fetch(`/generate-wt-image?text=${encodeURIComponent(text)}&format=discord`);
                            if (!response.ok) {
                                throw new Error("The image could not be generated.");
                            }

                            if (imageUrl) URL.revokeObjectURL(imageUrl);
                            imageUrl = URL.createObjectURL(await response.blob());
                            currentText = text.toLowerCase().replace(/\s+/g, "-");
                            preview.src = imageUrl;
                            previewBlock.hidden = false;
                            status.textContent = "";
                        } catch (error) {
                            status.textContent = error.message;
                        }
                    });
                </script>
            </body>
            </html>
            """,
            "text/html"));

        app.MapGet("/generate-wt-image", (HttpContext context) =>
        {
            var text = context.Request.Query["text"].ToString();
            if (string.IsNullOrWhiteSpace(text) ||
                text.Length > MaxTextLength ||
                text.Any(character =>
                    !char.IsAsciiLetterOrDigit(character) && character != ' '))
            {
                return Results.BadRequest(new
                {
                    error =
                        $"Text must contain only letters, numbers, or spaces and be at most {MaxTextLength} characters long."
                });
            }

            try
            {
                var discordReady = context.Request.Query["format"] == "discord";
                return Results.File(Generate(text, discordReady), "image/png");
            }
            catch (FileNotFoundException)
            {
                return Results.Problem("The sprite sheet is not available.", statusCode: 503);
            }
        });
    }

    private static byte[] Generate(string text, bool discordReady)
    {
        var spritePath = Path.Combine(AppContext.BaseDirectory, "Sprites", SpriteFileName);
        using var sheet = Image.Load<Rgba32>(spritePath);
        var glyphs = ReadGlyphs(sheet);
        var canvasWidth = discordReady ? DiscordWidth : 0;
        var canvasHeight = discordReady ? DiscordHeight : 0;
        var contentWidth = 0;
        var contentHeight = 0;
        var advances = new List<(Image<Rgba32>? Glyph, int Width)>(text.Length);

        foreach (var character in text.ToUpperInvariant())
        {
            if (character == ' ')
            {
                var spaceWidth = Math.Max(24, glyphs.Values.Max(glyph => glyph.Width) / 2);
                advances.Add((null, spaceWidth));
                contentWidth += spaceWidth;
                continue;
            }

            var glyph = glyphs[character];
            advances.Add((glyph, glyph.Width));
            contentWidth += glyph.Width;
            contentHeight = Math.Max(contentHeight, glyph.Height);
        }

        contentWidth += LetterSpacing * Math.Max(0, advances.Count - 1);
        if (!discordReady)
        {
            canvasWidth = contentWidth + Padding * 2;
            canvasHeight = contentHeight + Padding * 2;
        }

        var availableWidth = canvasWidth - Padding * 2;
        var availableHeight = canvasHeight - Padding * 2;
        var scale = discordReady
            ? Math.Min(1f, Math.Min(
                availableWidth / (float)Math.Max(contentWidth, 1),
                availableHeight / (float)Math.Max(contentHeight, 1)))
            : 1f;
        var scaledSpacing = (int)Math.Round(LetterSpacing * scale);
        var scaledContentWidth = (int)Math.Round(contentWidth * scale) -
                                 LetterSpacing * Math.Max(0, advances.Count - 1) +
                                 scaledSpacing * Math.Max(0, advances.Count - 1);

        using var result = new Image<Rgba32>(canvasWidth, canvasHeight, Color.Transparent);
        var x = (canvasWidth - scaledContentWidth) / 2;
        for (var index = 0; index < advances.Count; index++)
        {
            var (glyph, advance) = advances[index];
            if (glyph is not null)
            {
                var scaledWidth = Math.Max(1, (int)Math.Round(glyph.Width * scale));
                var scaledHeight = Math.Max(1, (int)Math.Round(glyph.Height * scale));
                using var rendered = scale == 1f
                    ? glyph.Clone()
                    : glyph.Clone(context => context.Resize(scaledWidth, scaledHeight));
                var y = (canvasHeight - rendered.Height) / 2;
                result.Mutate(context => context.DrawImage(rendered, new Point(x, y), 1f));
                x += scaledWidth;
            }
            else
            {
                x += Math.Max(1, (int)Math.Round(advance * scale));
            }

            if (index < advances.Count - 1)
            {
                x += scaledSpacing;
            }
        }

        try
        {
            using var output = new MemoryStream();
            result.Save(output, new PngEncoder());
            return output.ToArray();
        }
        finally
        {
            foreach (var glyph in glyphs.Values)
            {
                glyph.Dispose();
            }
        }
    }

    private static Dictionary<char, Image<Rgba32>> ReadGlyphs(Image<Rgba32> sheet)
    {
        var glyphs = new Dictionary<char, Image<Rgba32>>(ExpectedGlyphCount);
        var rows = WordGenerator.FindGlyphRows(sheet, GridRows);
        foreach (var (top, bottom) in rows)
        {
            var runs = WordGenerator.FindGlyphRuns(sheet, top, bottom + 1);
            foreach (var (left, right) in runs)
            {
                var (glyphTop, glyphBottom) = WordGenerator.FindGlyphVerticalBounds(
                    sheet,
                    left,
                    right,
                    top,
                    bottom + 1);
                var glyph = sheet.Clone(context => context.Crop(new Rectangle(
                    left,
                    glyphTop,
                    right - left + 1,
                    glyphBottom - glyphTop + 1)));
                glyphs.Add(
                    GlyphOrder[glyphs.Count],
                    WordGenerator.TrimTransparentMargins(glyph));
            }
        }

        if (glyphs.Count != ExpectedGlyphCount)
        {
            foreach (var glyph in glyphs.Values)
            {
                glyph.Dispose();
            }

            throw new InvalidOperationException(
                $"The sprite sheet does not contain {ExpectedGlyphCount} glyphs.");
        }

        return glyphs;
    }
}