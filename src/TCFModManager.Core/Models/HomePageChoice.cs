namespace TCFModManager.Core.Models;

//
// Fork (SSPTMM 1.4.2): the page the window opens on and the hub's Home button goes to - Steam's
// Settings > Interface > "Start up location". Written as a name, as WindowStartupMode is.
//
public enum HomePageChoice
{
    // The Workshop's front page: where the window always opened before there was a choice.
    Workshop,

    Browse,

    SubscribedItems,

    Collections,

    Play,
}
