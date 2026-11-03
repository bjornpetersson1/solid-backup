using System.Globalization;

using SolidBackup.Accompaniment;
using SolidBackup.Harmony;
using SolidBackup.Live;

using Spectre.Console;
using Spectre.Console.Rendering;

namespace SolidBackup.Cli;

/// <summary>What the dashboard shows besides the snapshot: where q and Esc lead, and whether the output is the built-in speakers.</summary>
public sealed record DashboardContext(bool ReturnsToMenu, bool OnSpeakers);

/// <summary>
/// The live dashboard: one panel of labelled rows built from a <see cref="LiveSnapshot"/>, in Swedish, with how to
/// leave live mode under it. It is a pure function of the snapshot, the context and the width, so it can be tested
/// without audio; the level meter takes the width that is left, so every row fits on one line from 80 columns up.
/// </summary>
public static class LiveDashboard
{
    public const string Title = "solid-backup · live";
    public const double MeterFloorDbfs = -60;

    /// <summary>The speaker warning as the panel shows it, short enough for one row at 80 columns.</summary>
    public const string SpeakerRow = "ljudet går till de inbyggda högtalarna – använd hörlurar";

    private const int MinMeterWidth = 10;
    private const int MaxMeterWidth = 40;

    // The panel's two borders and its padding on each side.
    private const int PanelChrome = 4;
    private const int LabelGap = 2;

    // The level row's text after the meter at its widest, so the meter keeps its width as the level and gate change.
    private const string WidestLevelText = " -60.0 dBFS · grind stängd";

    private static readonly string[] Labels = ["Ton", "Ackord", "Tonart", "Nivå", "Stil", "Byten", "Autospel", "Funktioner", "Latens", "Avbrott", "Varning"];
    private static readonly int LabelWidth = Labels.Max(label => label.Length);

    public static IRenderable Render(LiveSnapshot snapshot, DashboardContext context, int width)
    {
        Grid rows = new Grid().AddColumn(new GridColumn().Width(LabelWidth).NoWrap().PadRight(LabelGap)).AddColumn(new GridColumn().NoWrap());
        int meterWidth = Math.Clamp(width - PanelChrome - LabelWidth - LabelGap - WidestLevelText.Length, MinMeterWidth, MaxMeterWidth);
        AddRow(rows, "Ton", Markup.Escape(snapshot.State.Estimate.ToString()));
        AddRow(rows, "Ackord", Markup.Escape(Chord(snapshot.State)));
        AddRow(rows, "Tonart", Markup.Escape(Key(snapshot)));
        AddRow(rows, "Nivå", Level(snapshot, meterWidth));
        AddRow(rows, "Stil", Markup.Escape(Style(snapshot)));
        AddRow(rows, "Byten", Markup.Escape(SettingTexts.ChordChanges(snapshot.Settings)));
        AddRow(rows, "Autospel", SettingTexts.OnOff(snapshot.Settings.Autoplay));
        AddRow(rows, "Funktioner", Markup.Escape(Features(snapshot)));
        AddRow(rows, "Latens", Latency(snapshot));
        AddRow(rows, "Avbrott", Dropouts(snapshot));
        if (context.OnSpeakers)
        {
            AddRow(rows, "Varning", $"[yellow]{SpeakerRow}[/]");
        }

        Panel panel = new Panel(rows).Header(Title).Border(BoxBorder.Rounded).Expand();
        return new Rows(panel, new Text(Footer(context)));
    }

    /// <summary>The line under the panel: how to leave live mode.</summary>
    public static string Footer(DashboardContext context)
    {
        return context.ReturnsToMenu ? "q/Esc: tillbaka till menyn" : "q/Esc: avsluta";
    }

    /// <summary>
    /// A level meter of <paramref name="width"/> cells from −60 to 0 dBFS, with the gate threshold marked │; a
    /// threshold below the meter's floor is not marked.
    /// </summary>
    public static string Meter(double levelDbfs, double gateThresholdDbfs, int width)
    {
        int filled = Cell(levelDbfs, width);
        int gate = gateThresholdDbfs < MeterFloorDbfs ? -1 : Math.Min(Cell(gateThresholdDbfs, width), width - 1);
        char[] cells = new char[width];
        for (int i = 0; i < width; i++)
        {
            cells[i] = i == gate ? '│' : i < filled ? '█' : '░';
        }

        return new string(cells);
    }

    private static void AddRow(Grid rows, string label, string markup)
    {
        rows.AddRow(new Markup($"[bold]{label}[/]"), new Markup(markup));
    }

    private static int Cell(double dbfs, int width)
    {
        double fraction = (Math.Clamp(dbfs, MeterFloorDbfs, 0) - MeterFloorDbfs) / -MeterFloorDbfs;
        return (int)Math.Round(fraction * width);
    }

    private static string Chord(LiveState state)
    {
        return state.Chord is { } chord ? chord.Symbol : "–";
    }

    private static string Key(LiveSnapshot snapshot)
    {
        if (snapshot.State.Key is not { } key)
        {
            return "–";
        }

        string name = HarmonyNames.Key(key);
        return snapshot.Settings.Key == HarmonySettings.AutoKey ? $"{name} (auto)" : name;
    }

    private static string Level(LiveSnapshot snapshot, int meterWidth)
    {
        string meter = Meter(snapshot.State.LevelDbfs, snapshot.Settings.GateThresholdDbfs, meterWidth);
        double dbfs = snapshot.State.LevelDbfs;
        string level = dbfs < MeterFloorDbfs || !double.IsFinite(dbfs)
            ? $"<{MeterFloorDbfs.ToString("0", CultureInfo.InvariantCulture)} dBFS"
            : $"{dbfs.ToString("0.0", CultureInfo.InvariantCulture)} dBFS";
        string gate = snapshot.Settings.GateOn ? (snapshot.State.GateOpen ? " · grind öppen" : " · grind stängd") : "";
        return Markup.Escape($"{meter} {level}{gate}");
    }

    private static string Style(LiveSnapshot snapshot)
    {
        return $"{SettingTexts.Style(snapshot.Settings)} · {snapshot.Settings.Sound} · {SettingTexts.Rhythm(snapshot.Settings)}";
    }

    private static string Features(LiveSnapshot snapshot)
    {
        return string.Join("  ", LevelFeatures.All.Select(entry => $"{entry.DisplayName} {(entry.IsOn(snapshot.Settings) ? "✓" : "✗")}"));
    }

    private static string Latency(LiveSnapshot snapshot)
    {
        LiveLatency latency = snapshot.Latency;
        int target = snapshot.Settings.LatencyTargetMs;
        string total = latency.TotalMs is null ? "? ms"
            : latency.Meets(target) ? $"[green]{LatencyTexts.Total(latency)} ms ✓[/]"
            : $"[red]{LatencyTexts.Total(latency)} ms ✗[/]";
        return $"{total} mål {target} · in {LatencyTexts.Ms(latency.InputMs)} · kedja {LatencyTexts.Chain(latency)} · "
            + $"överl. {LatencyTexts.Ms(latency.HandOverMs)} · ut {LatencyTexts.Ms(latency.OutputMs)}";
    }

    private static string Dropouts(LiveSnapshot snapshot)
    {
        long dropouts = snapshot.Counters.Dropouts;
        string count = dropouts > 0 ? $"[red bold]{dropouts}[/]" : "0";
        if (snapshot.Recording is not { } recording)
        {
            return count;
        }

        TimeSpan length = TimeSpan.FromSeconds(recording.Seconds);
        return $"{count}   Inspelning {(int)length.TotalMinutes:00}:{length.Seconds:00} (tappade {recording.Overruns})";
    }
}
