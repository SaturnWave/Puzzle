using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Puzzle.Core.Model;

namespace Puzzle.Core.Tests;

/// <summary>
/// The player page of the public mode as the files a browser loads: every file the page, its scripts and its
/// stylesheet name is served, the art it draws with is whole and holds nothing the server's policy drops, and the
/// one thing the page brings from another site, its typefaces, is exactly what the policy lets in. The page draws
/// the board with the lab's own script but its own stylesheet and frame, so what those two take from the shared
/// script — the pace of every step, the marks on a cell — is pinned here as well. No test here runs the page's
/// scripts; they pin what the page is made of.
/// </summary>
public sealed class PlayerPageTests : IDisposable
{
    /// <summary>The page asks Google Fonts for its typefaces: a stylesheet from one host, which names font files on another.</summary>
    private const string FontStylesheets = "https://fonts.googleapis.com";

    private const string FontFiles = "https://fonts.gstatic.com";

    /// <summary>The file the page's art module fetches; it is named in a string, not in an import.</summary>
    private const string Sprite = "pieces.svg";

    private const string PlayerStylesheet = "/gallery.css";
    private const string LabStylesheet = "/app.css";
    private const string SharedBoard = "/game-view.js";
    private const string BoardFrame = "/framed-board.js";

    /// <summary>The lengths and curves of the board's steps; the shared board waits the same steps out in its script.</summary>
    private static readonly string[] BoardTimings = ["--move-ms", "--swap-ms", "--fade-ms", "--ease-swap", "--ease-fall"];

    /// <summary>The rules that move a tile in a step, by their selectors as both stylesheets write them.</summary>
    private static readonly string[] BoardStepRules =
    [
        ".board[data-phase=\"swap\"] .tile",
        ".board[data-phase=\"fall\"] .tile",
        ".board.still .tile, .tile.instant",
        ".tile.dying",
    ];

    /// <summary>What the shared board writes on its elements that the page's frame reads back: where a cell lies and how large it is.</summary>
    private static readonly string[] CellPlacement = ["--cell-size", "--x", "--y"];

    /// <summary>The classes the shared board puts on a cell for its varnish; the page's stylesheet shows the slabs by them.</summary>
    private static readonly string[] VarnishMarks = ["jelly-1", "jelly-2", "jelly-hit", "jelly-clear"];

    /// <summary>What SVG markup and the scripts that build it name their elements by: an address in form only, nothing is loaded from it.</summary>
    private const string SvgNamespace = "http://www.w3.org/2000/svg";

    private readonly TempFolder _folder = new();

    public PlayerPageTests()
    {
        PublicLevels.Write(Path.Combine(_folder.Path, "levels"), PublicLevels.Easy(1));
    }

    public void Dispose() => _folder.Dispose();

    [Fact]
    public async Task Every_file_the_player_page_its_scripts_and_its_stylesheet_name_is_served()
    {
        await using PuzzleServer server = await PuzzleServer.StartPublicAsync(_folder.Path);
        TestBrowser browser = server.NewBrowser();

        IReadOnlyDictionary<string, string> files = await FilesOfThePageAsync(browser);

        // Named here once, so that a reader of the page which finds nothing fails instead of passing on an empty list.
        string[] expected =
        {
            "alcove.js", "board-outline.js", "dom.js", "framed-board.js", "gallery.css", "gallery.js", "game-view.js", "hud.js",
            "icon.svg", "piece-art.js", Sprite, "result-panel.js", "wall.svg",
        };
        Assert.Equal(expected, files.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task The_player_pages_art_has_a_symbol_for_every_tile_cover_and_goal_the_page_draws()
    {
        await using PuzzleServer server = await PuzzleServer.StartPublicAsync(_folder.Path);
        using HttpResponseMessage answer = await server.NewBrowser().GetAsync("/" + Sprite);
        string sprite = await answer.Content.ReadAsStringAsync();

        // The board (game-view.js) names a gem's symbol after its colour, the HUD (hud.js) a colour's goal likewise.
        string[] colours = Enum.GetNames<GemColor>().Select(colour => colour.ToLowerInvariant()).ToArray();
        string[] others =
        {
            "sp-rocket-h", "sp-rocket-v", "sp-bomb", "sp-bigbomb", "sp-seeker", "sp-colorbomb",
            "sp-box-1", "sp-box-2", "sp-box-3", "item", "ov-chain-1", "ov-chain-2", "unknown",
            "slab-1-0", "slab-1-1", "slab-1-2", "slab-2-0", "slab-2-1", "slab-2-2", "exit",
            "goal-boxes", "goal-jelly", "goal-chains", "goal-items", "mark-star", "sconce",
        };

        Assert.Equal((HttpStatusCode.OK, "image/svg+xml"), (answer.StatusCode, answer.Content.Headers.ContentType?.MediaType));
        Assert.Contains("<svg class=\"sprite\"", sprite);
        foreach (string symbol in colours.Select(colour => $"gem-{colour}").Concat(colours.Select(colour => $"goal-{colour}")).Concat(others))
        {
            Assert.Contains($"<symbol id=\"{symbol}\"", sprite);
        }
    }

    [Fact]
    public async Task The_player_pages_art_holds_nothing_the_policy_drops_and_points_at_nothing_it_lacks()
    {
        await using PuzzleServer server = await PuzzleServer.StartPublicAsync(_folder.Path);
        using HttpResponseMessage answer = await server.NewBrowser().GetAsync("/" + Sprite);
        string sprite = await answer.Content.ReadAsStringAsync();

        string[] ids = Regex.Matches(sprite, "\\sid=\"([^\"]+)\"").Select(match => match.Groups[1].Value).ToArray();
        IEnumerable<string> pointedAt = Regex.Matches(sprite, "href=\"#([^\"]+)\"|url\\(#([^)]+)\\)")
            .Select(match => match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value)
            .Distinct();

        // The sprite is put into the page itself, where the policy drops inline styles and scripts without a word.
        Assert.DoesNotMatch("\\sstyle=|<style|<script|\\son[a-z]+=|data:", sprite);
        Assert.DoesNotContain("<filter", sprite); // a filter is drawn again for every tile that shows it
        Assert.Equal(ids.Length, ids.Distinct().Count()); // two definitions under one id: the first one paints both
        Assert.Empty(pointedAt.Except(ids)); // a paint that is not there paints nothing
    }

    [Fact]
    public async Task A_public_server_lets_in_one_thing_of_another_site_the_player_pages_typefaces()
    {
        await using PuzzleServer server = await PuzzleServer.StartPublicAsync(_folder.Path);
        TestBrowser browser = server.NewBrowser();

        using HttpResponseMessage page = await browser.GetAsync("/");
        string policy = Assert.Single(page.Headers.GetValues("Content-Security-Policy"));
        Dictionary<string, string[]> directives = policy.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(directive => directive.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToDictionary(words => words[0], words => words[1..]);
        (string Directive, string Source)[] fromOtherSites = directives
            .SelectMany(directive => directive.Value.Where(source => !source.StartsWith('\'')).Select(source => (directive.Key, source)))
            .Order()
            .ToArray();

        Assert.Equal(new[] { "'self'" }, directives["default-src"]);
        Assert.DoesNotContain("script-src", directives.Keys); // scripts keep to the default: this server's own files
        Assert.Equal(new[] { ("font-src", FontFiles), ("style-src", FontStylesheets) }, fromOtherSites);
    }

    [Fact]
    public async Task The_only_address_of_another_site_in_the_player_pages_files_is_the_font_stylesheet()
    {
        await using PuzzleServer server = await PuzzleServer.StartPublicAsync(_folder.Path);
        TestBrowser browser = server.NewBrowser();
        using HttpResponseMessage page = await browser.GetAsync("/");
        IReadOnlyDictionary<string, string> files = await FilesOfThePageAsync(browser);

        IEnumerable<string> texts = files.Values.Append(await page.Content.ReadAsStringAsync());
        IEnumerable<string> addresses = texts
            .SelectMany(text => Regex.Matches(text, "https?://[^\"'\\s)]+").Select(match => match.Value))
            .Where(address => address != SvgNamespace)
            .Distinct();

        string address = Assert.Single(addresses);
        Assert.StartsWith(FontStylesheets + "/css2?", address, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_player_page_moves_the_board_at_the_pace_the_shared_board_waits_for()
    {
        await using PuzzleServer server = await PuzzleServer.StartPublicAsync(_folder.Path);
        TestBrowser browser = server.NewBrowser();
        string labs = await ReadAsync(browser, LabStylesheet);
        string players = await ReadAsync(browser, PlayerStylesheet);

        // The lab's stylesheet is the one kept in step with the script's waits; the player page's must say the same.
        foreach (string timing in BoardTimings)
        {
            Assert.Equal((timing, ValueOf(labs, timing)), (timing, ValueOf(players, timing)));
        }

        foreach (string selector in BoardStepRules)
        {
            Assert.Equal((selector, RuleOf(labs, selector)), (selector, RuleOf(players, selector)));
        }
    }

    [Fact]
    public async Task The_shared_board_marks_its_cells_the_way_the_player_pages_frame_and_stylesheet_read_them()
    {
        await using PuzzleServer server = await PuzzleServer.StartPublicAsync(_folder.Path);
        TestBrowser browser = server.NewBrowser();
        string board = await ReadAsync(browser, SharedBoard);
        string frame = await ReadAsync(browser, BoardFrame);
        string stylesheet = await ReadAsync(browser, PlayerStylesheet);

        // A name changed in the shared board alone would leave the frame or the slabs undrawn without an error.
        foreach (string placement in CellPlacement)
        {
            Assert.Contains($"\"{placement}\"", board);
            Assert.Contains($"\"{placement}\"", frame);
        }

        Assert.Contains("make(\"div\", `cell", board);
        Assert.Contains(":scope > .cell", frame);
        foreach (string mark in VarnishMarks)
        {
            Assert.Contains($"\"{mark}\"", board);
            Assert.Contains($".cell.{mark} ", stylesheet);
        }
    }

    private static async Task<string> ReadAsync(TestBrowser browser, string path)
    {
        using HttpResponseMessage served = await browser.GetAsync(path);
        Assert.True(served.StatusCode == HttpStatusCode.OK, $"GET {path} answered {served.StatusCode}");
        return await served.Content.ReadAsStringAsync();
    }

    /// <summary>The value a stylesheet first gives the custom property <paramref name="name"/>.</summary>
    private static string ValueOf(string stylesheet, string name)
    {
        Match declared = Regex.Match(stylesheet, Regex.Escape(name) + @":\s*([^;]+);");
        Assert.True(declared.Success, $"The stylesheet declares no {name}.");
        return declared.Groups[1].Value.Trim();
    }

    /// <summary>The declarations of the rule a stylesheet writes under exactly <paramref name="selector"/>, spaces evened out.</summary>
    private static string RuleOf(string stylesheet, string selector)
    {
        Match rule = Regex.Match(stylesheet, @"(?m)^\s*" + Regex.Escape(selector) + @"\s*\{([^}]*)\}");
        Assert.True(rule.Success, $"The stylesheet has no rule for {selector}.");
        return Regex.Replace(rule.Groups[1].Value, @"\s+", " ").Trim();
    }

    /// <summary>
    /// Every file the player page leads to, by name, with its content: what the page's markup names, what those
    /// scripts import and the stylesheet lays as a picture, and so on from each, and the sprite the art module fetches.
    /// Each must be served, as the kind of file its name says.
    /// </summary>
    private static async Task<IReadOnlyDictionary<string, string>> FilesOfThePageAsync(TestBrowser browser)
    {
        using HttpResponseMessage page = await browser.GetAsync("/");
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        var toRead = new Queue<string>(NamesIn(await page.Content.ReadAsStringAsync(), "(?:href|src)=\"([^\"#:]+)\"").Append(Sprite));
        while (toRead.TryDequeue(out string? name))
        {
            if (files.ContainsKey(name))
            {
                continue;
            }

            using HttpResponseMessage served = await browser.GetAsync("/" + name);
            Assert.True(served.StatusCode == HttpStatusCode.OK, $"GET /{name} answered {served.StatusCode}");
            Assert.Equal(MediaTypeOf(name), served.Content.Headers.ContentType?.MediaType);
            string content = await served.Content.ReadAsStringAsync();
            files.Add(name, content);
            foreach (string named in NamesIn(content, NamedFilesPattern(name)))
            {
                toRead.Enqueue(named);
            }
        }

        return files;
    }

    /// <summary>How a file of this kind names other files: a script by its imports, a stylesheet by its pictures; a picture names none.</summary>
    private static string NamedFilesPattern(string name) => Path.GetExtension(name) switch
    {
        ".js" => "from \"\\./([^\"]+)\"",
        ".css" => "url\\(\"([^\"#:]+)\"\\)",
        _ => "(?!)",
    };

    private static IEnumerable<string> NamesIn(string text, string pattern) =>
        Regex.Matches(text, pattern).Select(match => match.Groups[1].Value);

    private static string MediaTypeOf(string name) => Path.GetExtension(name) switch
    {
        ".js" => "text/javascript",
        ".css" => "text/css",
        ".svg" => "image/svg+xml",
        _ => throw new ArgumentException($"The player page names '{name}', a kind of file these tests do not know.", nameof(name)),
    };
}
