using TCFModManager.App.ViewModels;
using TCFModManager.Core.Services;

namespace TCFModManager.App.Services;

//
// Fork (1.4.2): names you gave installed mods, in AppSettings.ModNames. A mod sp-mod.com lists is
// kept by its id, so the name follows it through updates that rename its folder; any other by its
// folder's name. Addons are numbered apart from mods, so theirs is "addon:".
//
public static class ModNicknames
{
    public static string KeyFor(InstalledModCardViewModel card) =>
        card.ModId is > 0 and var id ? (card.IsAddon ? "addon:" : "id:") + id : "name:" + card.Name.ToLowerInvariant();

    public static void Apply(IEnumerable<InstalledModCardViewModel> cards)
    {
        var names = new SettingsService().Load().ModNames;
        foreach (var card in cards) card.CustomName = names.GetValueOrDefault(KeyFor(card));
    }

    public static void Set(InstalledModCardViewModel card, string? name)
    {
        var service = new SettingsService();
        var settings = service.Load();
        if (name is null) settings.ModNames.Remove(KeyFor(card));
        else settings.ModNames[KeyFor(card)] = name;
        service.Save(settings);

        card.CustomName = name;
        AppLog.Info("Installed", name is null ? $"{card.Name}: own name back" : $"{card.Name} renamed");
    }
}
