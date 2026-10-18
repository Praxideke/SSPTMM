using System.Windows;

namespace TCFModManager.App.Behaviors;

//
// Fork (1.4.3): Options > Look > Animations. Off, a Workshop card no longer rises under the pointer
// and pages change without sliding in. Smooth scrolling has its own switch. The cards read the two
// resources as they are made, so pages already open keep the old way until SSPTMM is restarted.
//
public static class Motion
{
    public static bool Enabled { get; private set; } = true;

    public static void Apply(bool enabled)
    {
        Enabled = enabled;
        if (Application.Current is not { } app) return;

        app.Resources["CardLiftOffset"] = enabled ? -5.0 : 0.0;
        app.Resources["CardLiftDuration"] = new Duration(enabled ? TimeSpan.FromSeconds(0.15) : TimeSpan.Zero);
    }
}
