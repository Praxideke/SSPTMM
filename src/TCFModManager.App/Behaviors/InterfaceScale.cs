using System.Windows;
using System.Windows.Media;
using TCFModManager.Core.Services;

namespace TCFModManager.App.Behaviors;

//
// Fork (1.4.2): Options > Look > Interface size, and Ctrl+= / Ctrl+- / Ctrl+0 - everything in the
// window drawn larger or smaller, as Steam's own setting does. A LayoutTransform on the window's
// content, so pages lay themselves out again at the new size rather than being stretched. Menus,
// tooltips and dialogs are windows of their own and keep their size.
//
public static class InterfaceScale
{
    // HUNCH: Steam offers 100% upwards; 80% and 90% are here for a small laptop screen with long
    // labels, 200% as the most a 1080p screen holds at the window's minimum size.
    public static IReadOnlyList<double> Steps { get; } = [0.8, 0.9, 1.0, 1.1, 1.25, 1.5, 1.75, 2.0];

    public static double Current { get; private set; } = 1.0;

    /// <summary>Raised after every change, from a key or from Options.</summary>
    public static event EventHandler? Changed;

    private static FrameworkElement? _content;

    /// <summary>The window's content, scaled from now on; at the size saved last time.</summary>
    public static void Attach(FrameworkElement content)
    {
        _content = content;
        Apply(new SettingsService().Load().InterfaceScale, save: false);
    }

    public static void Set(double scale) => Apply(scale, save: true);

    /// <summary>One step larger (+1) or smaller (-1).</summary>
    public static void Step(int direction)
    {
        var next = direction > 0
            ? Steps.FirstOrDefault(s => s > Current + 0.001, Steps[^1])
            : Steps.LastOrDefault(s => s < Current - 0.001, Steps[0]);
        Set(next);
    }

    private static void Apply(double scale, bool save)
    {
        if (double.IsNaN(scale) || scale <= 0) scale = 1.0;
        scale = Math.Clamp(Math.Round(scale, 2), Steps[0], Steps[^1]);
        if (save && Math.Abs(scale - Current) < 0.001) return;

        Current = scale;
        if (_content is not null)
            _content.LayoutTransform = Math.Abs(scale - 1.0) < 0.001 ? Transform.Identity : new ScaleTransform(scale, scale);

        if (save)
        {
            var service = new SettingsService();
            var settings = service.Load();
            settings.InterfaceScale = scale;
            service.Save(settings);
            AppLog.Info("Options", $"interface size {scale * 100:0}%");
        }

        Changed?.Invoke(null, EventArgs.Empty);
    }
}
