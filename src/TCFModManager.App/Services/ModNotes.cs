using TCFModManager.App.ViewModels;
using TCFModManager.Core.Services;

namespace TCFModManager.App.Services;

//
// Fork (1.4.3): a note of your own on an installed mod - "needs config X", "broke with Realism" -
// shown on its card and found by the search box. Kept in AppSettings.ModNotes under the same key
// as a name you gave it (ModNicknames), so it follows a listed mod through updates.
//
public static class ModNotes
{
    public static void Apply(IEnumerable<InstalledModCardViewModel> cards)
    {
        var notes = new SettingsService().Load().ModNotes;
        foreach (var card in cards) card.Note = notes.GetValueOrDefault(ModNicknames.KeyFor(card));
    }

    public static void Set(InstalledModCardViewModel card, string? note)
    {
        var service = new SettingsService();
        var settings = service.Load();
        if (note is null) settings.ModNotes.Remove(ModNicknames.KeyFor(card));
        else settings.ModNotes[ModNicknames.KeyFor(card)] = note;
        service.Save(settings);

        card.Note = note;
        AppLog.Info("Installed", note is null ? $"{card.Name}: note removed" : $"{card.Name}: note saved");
    }
}
