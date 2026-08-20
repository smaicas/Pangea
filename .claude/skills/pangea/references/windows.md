# Windows and dialogs

What a window has to get right beyond its contents. Read this before writing one.

## Windows have to fit on smaller screens

A window is opened at a size that suits the developer's monitor and then used on someone else's.
Make the part that holds content scroll, and **choose where the scroll goes** rather than wrapping
the whole window: inside a `ScrollViewer` the available height becomes infinite, so `*` rows
collapse and anything that stretches stops stretching.

```xml
<Grid RowDefinitions="Auto,*">
  <Border Grid.Row="0"> <!-- header, stays put --> </Border>

  <ScrollViewer Grid.Row="1">   <!-- only the content region scrolls -->
    <StackPanel> <!-- ... --> </StackPanel>
  </ScrollViewer>
</Grid>
```

For a side panel next to a filling region, scroll the panel alone and leave the region to fill.
The toolkit does not do this for you, deliberately: only the author knows which part should scroll
and which should stretch.

## Keyboard, for windows and dialogs alike

- **Both get focus placed for them** when they open, on the first control that can take it - unless
  the window focused something itself, which is always respected.
- **Escape closes a dialog. It does not close a window**, and that is the platform convention, not
  an oversight: Alt+F4 closes windows, and Escape destroying one holding unsaved work would be a
  keystroke away from losing it. For a secondary window where Alt+F4 feels absurd, ask for it:

```xml
<Window xmlns:win="using:CdCSharp.Pangea.Windows"
        win:WindowBehavior.CloseOnEscape="True">
```

## What a dialog is for

`IDialogService` asks two questions - a confirmation and a statement - and nothing else. For a
dialog with its own fields, or a result that is not a bool, write a view model and a window and use
`IWindowManager.ShowDialogAsync<TWindow, TViewModel, TResult>`.

Dismissing a dialog by its window chrome is read as a cancel, so `ConfirmAsync` returns `false`. It
never returns `true` without the user saying so. A dialog needs a main window to own it, and says so
if there is none. It takes the application's theme; there is nothing to style.
