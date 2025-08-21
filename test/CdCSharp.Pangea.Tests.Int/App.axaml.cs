using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CdCSharp.Pangea.Tests.Int.ViewModels;
using CdCSharp.Pangea.Tests.Int.Views;
using CdCSharp.Pangea.Theming;
using System;

namespace CdCSharp.Pangea.Tests.Int;

public partial class App : PangeaApplication
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }
}