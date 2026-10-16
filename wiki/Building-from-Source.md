# Building from source

## The easy way

1. Get the code: **Code > Download ZIP** on GitHub, or `git clone https://github.com/trappuss/SSPTMM`.
2. Double-click **`SSPTMM-build-and-run.bat`**. It:
   1. brings in an update bundle, if there is one in `Claude outputs\` (the maintainer's workflow -
      skipped otherwise);
   2. finds a .NET 9 SDK, or installs one into `.dotnet\` beside the file (no admin rights);
   3. builds the app;
   4. runs the tests - a failure stops here;
   5. publishes a self-contained `SSPTMM.exe` into `dist\SSPTMM\`;
   6. starts it.

Everything it does is written to `logs\build.log`.

For the maintainer, **`SSPTMM-upload-to-github.bat`** sends the code and wiki to GitHub, and makes
the release when the version is new (`logs\upload.log`).

## By hand

```
dotnet build src\TCFModManager.App -c Release
dotnet test Tests\TCFModManager.Core.Tests -c Release
dotnet publish src\TCFModManager.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist\SSPTMM
```

## Layout

| Folder | What |
|---|---|
| `src\TCFModManager.Core` | Everything that isn't UI: sp-mod.com client, installing and removing, configs, logs, profiles. |
| `src\TCFModManager.App` | The WPF app (WPF-UI), its Steam theme, pages and strings (`Localization\Strings*.resx`). |
| `Tests\TCFModManager.Core.Tests` | xUnit tests for Core. |
| `docs\` | The development log (`steam-workshop-ui.md`), the Server Map guide, screenshots. |

The project and namespace names (`TCFModManager.*`) come from the app SSPTMM began as a fork of;
they are internal only. The version is set in one place: `build\Directory.Build.props`.

## Translations

The app's text is in `src\TCFModManager.App\Localization\Strings.resx`, with German, French,
Italian, Russian and Simplified Chinese (`zh-Hans`) beside it. Steam's own terms use Steam's own
translations. All five cover the whole app; they are machine-made, so corrections from native
speakers are welcome - pull requests or an issue.

Every build prints how complete each language is (`de: 2806/2806, complete`). Text added to
`Strings.resx` shows in English in a language until it is translated there. A placeholder such as
`{0}` must stay in a translation (the tests check it); a date pattern after the colon, as in
`{0:d MMM yyyy}`, may be changed to the language's own. A new language is a `Strings.<tag>.resx`
beside the others, its tag in `Meta_ShippedLanguages` and its plural rule in
`src\TCFModManager.Core\Services\PluralRules.cs`.
