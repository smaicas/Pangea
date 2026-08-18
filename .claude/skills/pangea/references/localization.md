# Localization keys

## Keys are checked against the .resx files

`GetString` returns the key itself when nothing resolves, so the application keeps working and shows
`Home_Title` to the user. That fallback is why a mistyped key can ship unnoticed.

An analyzer reads the project's `.resx` files and reports it first:

| | |
|---|---|
| `PGL001` | The resource key is in none of the project's `.resx` files |
| `PGL002` | The key is in the neutral `.resx` but missing from a translation |

Both are warnings. Neither defect has any other symptom: nothing else in the build, and nothing at
runtime, will ever mention a key that resolves to nothing or a language that is missing one.

## Changing language while the application runs

`SetCulture` changes the culture; nothing on screen re-reads its text unless something tells it to.
`LocalizedStrings` is what does — one object every binding goes through, registered by the feature,
which announces a change of culture as a change to every string it holds.

```csharp
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Localization;
using CdCSharp.Pangea.Localization.Controls;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace MyApp.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    public SettingsViewModel(IServiceProvider services) : base(services)
    {
        Strings = services.GetRequiredService<LocalizedStrings>();
        LanguageSelector = services.GetRequiredService<LanguageSelectorViewModel>();
    }

    // Bind labels through this and the whole window follows a change of language.
    public LocalizedStrings Strings { get; }

    // Drives the toolkit's picker. Choosing applies the language there and then.
    public LanguageSelectorViewModel LanguageSelector { get; }
}
```

```xml
<TextBlock Text="{Binding Strings[Settings_Title]}" />
<loc:LanguageSelector ViewModel="{Binding LanguageSelector}" />
```

with `xmlns:loc="using:CdCSharp.Pangea.Localization.Controls"`.

The picker lists the supported cultures by their **native** names, applies the choice immediately,
rolls back if the service refuses it, and follows a culture changed anywhere else. Set
`AutomationName` to give a screen reader a localized label for it.

**What it does not refresh:** anything the culture affects without going through
`LocalizedStrings` - a `StringFormat`, a date, a number. Those are formatted by the binding itself,
and a binding that has not been told anything changed will not run again. Re-raise those properties
from `CultureChanged` if a screen shows them.

## Marking your own wrappers

`GetString` declares its parameter as a key, and so does `LocalizedStrings`, so every
`Strings["..."]` is checked already. Put `[LocalizationKey]` on wrappers of your own and their call
sites are checked the same way:

```csharp
using CdCSharp.Pangea.Localization.Abstractions;

namespace MyApp.Localization;

public sealed class Greetings
{
    private readonly ILocalizationService _localization;

    public Greetings(ILocalizationService localization) => _localization = localization;

    // Greetings.For("Welcome_Back", name) is checked like any other key.
    public string For([LocalizationKey] string key, string name) =>
        string.Format(_localization.GetString(key), name);
}
```

Only constant keys are checked; one built at runtime is left alone. Keys written in XAML are not
seen either - they are not C# - but `PGL002` is about the resource files themselves, so it reports
whether or not any code reads the key.

## Changing the severity

Change the severity where a warning is not what the project wants, with a `.globalconfig` beside
it. A global config rather than `.editorconfig`, because `PGL002` is reported against `.resx` files
and once per compilation, which no path-based section matches reliably.

```ini
is_global = true

# Stricter, for a project where an untranslated string must not ship:
dotnet_diagnostic.PGL002.severity = error

# Or quieter, where translation lags the code on purpose:
# dotnet_diagnostic.PGL002.severity = suggestion
```

```xml
<ItemGroup>
  <GlobalAnalyzerConfigFiles Include="localization.globalconfig" />
</ItemGroup>
```

## Where the strings come from

`LocalizationOptions.ResourceAssemblies` names the assemblies holding the resource classes. The
service looks for a type with a public static `ResourceManager` property, which is what the `.resx`
designer emits — and what a hand-written class can expose instead:

```csharp
using System.Resources;

namespace MyApp.Resources;

public static class Strings
{
    public static ResourceManager ResourceManager { get; } =
        new("MyApp.Resources.Strings", typeof(Strings).Assembly);
}
```
