using System.Windows;
using System.Windows.Markup;

namespace TCFModManager.App.Look;

//
// Fork (1.4.2): {look:Brush #66C0F4} - a colour of the Steam look that follows Options > Look's
// accent colour and OLED black (AppLook). Written where the literal colour was, and drawn as it
// until either is changed.
//
[MarkupExtensionReturnType(typeof(object))]
public sealed class BrushExtension(string hex) : MarkupExtension
{
    public string Hex { get; set; } = hex;

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        LookResource.Provide(this, AppLook.Register(Hex, isColor: false), serviceProvider);
}

// {look:Color #1A9FFF}, for a Color property: a gradient stop, a brush's Color.
[MarkupExtensionReturnType(typeof(object))]
public sealed class ColorExtension(string hex) : MarkupExtension
{
    public string Hex { get; set; } = hex;

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        LookResource.Provide(this, AppLook.Register(Hex, isColor: true), serviceProvider);
}

internal static class LookResource
{
    //
    // A style's Setter takes a DynamicResource only as the extension itself, which it resolves
    // where the style is used. Inside a template the first call is made for the template as a
    // whole, not an element (the target is not a DependencyObject): handing back the extension
    // has it asked again for each element the template makes. Everything else takes the
    // reference the extension makes.
    //
    public static object Provide(MarkupExtension self, string key, IServiceProvider serviceProvider)
    {
        var resource = new DynamicResourceExtension(key);
        return (serviceProvider.GetService(typeof(IProvideValueTarget)) as IProvideValueTarget)?.TargetObject switch
        {
            Setter => resource,
            DependencyObject => resource.ProvideValue(serviceProvider),
            _ => self,
        };
    }
}
