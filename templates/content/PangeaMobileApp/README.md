# PangeaMobileApp

An Avalonia application for phones, built on [Pangea](https://github.com/smaicas/CdCSharp.Pangea).

## What is here

| Project | What it is |
|---|---|
| `PangeaMobileApp` | Everything the application is: views, view models, theme. |
| `PangeaMobileApp.Desktop` | An entry point, so a change can be seen without an emulator. |
<!--#if (Android) -->
| `PangeaMobileApp.Android` | The Android head. |
<!--#endif -->
<!--#if (iOS) -->
| `PangeaMobileApp.iOS` | The iOS head. |
<!--#endif -->

The heads hold an entry point and nothing else. Everything you write goes in the shared library.

## Running it

```bash
dotnet run --project PangeaMobileApp.Desktop
<!--#if (Android) -->
dotnet build -t:Run PangeaMobileApp.Android
<!--#endif -->
```

## What is already wired

- **A single-view shell.** `MainView` is what Android and iOS are handed; the desktop head wraps
  the same control in a `MainWindow`. A `Window` cannot be constructed on a phone at all.
- **The safe area.** The shell reads the platform inset, so the top bar is not under the clock.
- **Navigation** with a cross fade, and focus that does not summon the system keyboard on arrival.
- **A theme** declared as two C# palette classes in `Themes/AppPalette.cs`. Never edit theme XAML.
- **Per-platform storage** through `IStorageService`.

Read `.claude/skills/pangea/references/mobile.md` before adding a screen.
