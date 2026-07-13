namespace TourList.Client.Services;

/// <summary>
/// Fixed, visually-distinct palette for color-coding lists on the map + legend. When there are more
/// lists than colors, colors cycle (users rely on the legend + clustering to disambiguate).
/// </summary>
public static class ColorPalette
{
    public static readonly IReadOnlyList<string> Colors =
    [
        "#5eead4", "#f472b6", "#facc15", "#60a5fa", "#fb923c", "#a78bfa", "#4ade80", "#f87171",
        "#22d3ee", "#e879f9", "#a3e635", "#fbbf24", "#38bdf8", "#fca5a5", "#34d399", "#c084fc",
        "#2dd4bf", "#f9a8d4", "#fde047", "#93c5fd", "#fdba74", "#d8b4fe", "#86efac", "#fecaca",
    ];

    public static string ColorForList(int index) =>
        Colors[((index % Colors.Count) + Colors.Count) % Colors.Count];
}
