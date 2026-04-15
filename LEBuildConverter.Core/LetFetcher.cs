// ================================================================
//  LetFetcher.cs  -  lastepochtools.com build fetcher (C# port)
//
//  Given a LET planner slug (e.g. "BakypDvx" from the URL
//  https://www.lastepochtools.com/planner/BakypDvx), fetch the full
//  build JSON via their internal API.
//
//  NOTE: lastepochtools is behind Cloudflare which fingerprints the
//  TLS handshake (JA3) of .NET HttpClient and returns 403.  We use
//  curl.exe as a subprocess — it ships with every Windows 10+ install
//  at C:\Windows\System32\curl.exe and its TLS fingerprint matches
//  real browsers / Python urllib, so Cloudflare lets it through.
// ================================================================

using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LEBuildConverter.Core;

public static class LetFetcher
{
    private const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
        "AppleWebKit/537.36 (KHTML, like Gecko) " +
        "Chrome/131.0.0.0 Safari/537.36";

    private static readonly Regex HashRegex = new(
        @"rkv1_ydp\s*=\s*'([a-f0-9]{32})'",
        RegexOptions.Compiled);

    /// <summary>
    /// Fetch the lastepochtools planner build data for the given slug.
    /// Returns the root JSON object (typically with a top-level "data" key).
    /// </summary>
    public static async Task<JsonDocument> FetchBuildAsync(string slug, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
            throw new ArgumentException("slug required", nameof(slug));

        slug = ExtractSlug(slug);

        // Step 1: fetch the planner HTML to extract the rotating hash
        string plannerUrl = $"https://www.lastepochtools.com/planner/{slug}";
        string html = await CurlAsync(plannerUrl, headers: null, ct: ct).ConfigureAwait(false);

        var match = HashRegex.Match(html);
        if (!match.Success)
            throw new InvalidOperationException(
                $"Could not find rkv1_ydp hash in planner HTML for slug {slug}. " +
                "The build may not exist, or lastepochtools.com is down.");
        string digest = match.Groups[1].Value;

        // Step 2: fetch the JSON payload from the internal API
        string apiUrl = $"https://www.lastepochtools.com/api/internal/planner_data/{digest}";
        var headers = new Dictionary<string, string>
        {
            ["Referer"] = plannerUrl,
            ["X-Requested-With"] = "XMLHttpRequest",
            ["Accept"] = "application/json, text/javascript, */*; q=0.01",
        };
        string json = await CurlAsync(apiUrl, headers, ct).ConfigureAwait(false);

        return JsonDocument.Parse(json);
    }

    /// <summary>
    /// Accept either a full LET planner URL or a bare slug.
    /// Returns the slug.
    /// </summary>
    public static string ExtractSlug(string input)
    {
        input = input.Trim();
        const string marker = "/planner/";
        int idx = input.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            string rest = input[(idx + marker.Length)..];
            int cut = rest.IndexOfAny(new[] { '?', '#', '/' });
            if (cut >= 0) rest = rest[..cut];
            return rest;
        }
        return input;
    }

    // ── curl.exe subprocess wrapper ─────────────────────────────────────

    private static async Task<string> CurlAsync(
        string url,
        Dictionary<string, string>? headers,
        CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "curl.exe",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        psi.ArgumentList.Add("-s");                 // silent
        psi.ArgumentList.Add("-L");                 // follow redirects
        psi.ArgumentList.Add("--max-time");
        psi.ArgumentList.Add("30");
        psi.ArgumentList.Add("--compressed");       // accept gzip/brotli
        psi.ArgumentList.Add("-A");
        psi.ArgumentList.Add(UserAgent);

        if (headers is not null)
        {
            foreach (var (k, v) in headers)
            {
                psi.ArgumentList.Add("-H");
                psi.ArgumentList.Add($"{k}: {v}");
            }
        }

        psi.ArgumentList.Add(url);

        using var proc = new Process { StartInfo = psi };
        try
        {
            proc.Start();
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new InvalidOperationException(
                "curl.exe was not found. Windows 10 and later ship with curl.exe " +
                "at C:\\Windows\\System32\\curl.exe. If you're on older Windows, " +
                "install curl from https://curl.se/windows/.",
                ex);
        }

        string stdout = await proc.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
        string stderr = await proc.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
        await proc.WaitForExitAsync(ct).ConfigureAwait(false);

        if (proc.ExitCode != 0)
            throw new InvalidOperationException(
                $"curl failed (exit {proc.ExitCode}) for {url}: {stderr}");

        return stdout;
    }
}
