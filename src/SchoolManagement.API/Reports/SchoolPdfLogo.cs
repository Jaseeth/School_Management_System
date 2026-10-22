using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using SchoolManagement.Domain.Entities;

namespace SchoolManagement.API.Reports;

/// <summary>
/// Renders the configured school logo inside a fixed, proportional first-page header area.
/// Place this in page content (not Page.Header) so it does not repeat on later pages.
/// </summary>
public static class SchoolPdfLogo
{
    public static void Compose(IContainer container, SchoolProfile? school, string? fallbackPath = null)
    {
        var bytes = TryGetConfiguredLogo(school?.LogoDataUrl) ?? TryReadFallbackLogo(fallbackPath);
        if (bytes is null)
            return;

        container
            .AlignCenter()
            .Width(150)
            .Height(42)
            .Image(bytes)
            .FitArea();
    }

    private static byte[]? TryGetConfiguredLogo(string? dataUrl)
    {
        if (string.IsNullOrWhiteSpace(dataUrl))
            return null;

        var separator = dataUrl.IndexOf(',');
        if (separator < 0 || separator == dataUrl.Length - 1)
            return null;

        try
        {
            return Convert.FromBase64String(dataUrl[(separator + 1)..]);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static byte[]? TryReadFallbackLogo(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        try
        {
            return File.ReadAllBytes(path);
        }
        catch (IOException)
        {
            return null;
        }
    }
}
