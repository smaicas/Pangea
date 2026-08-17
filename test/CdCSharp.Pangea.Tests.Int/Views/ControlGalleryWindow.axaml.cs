using Avalonia.Controls;
using CdCSharp.Pangea.Theming.Controls;
using System.Collections.Generic;

namespace CdCSharp.Pangea.Tests.Int.Views;

public partial class ControlGalleryWindow : Window
{
    public ControlGalleryWindow()
    {
        InitializeComponent();
        FillSampleData();
    }

    /// <summary>Row shape for the TableView sample.</summary>
    public sealed record GalleryRow(string Name, string Role, int Count);

    /// <summary>
    /// Hands the window the shared theme selector so switching theme here drives the whole app.
    /// </summary>
    public void UseThemeSelector(ThemeSelectorViewModel selector) => SelectorHost.DataContext = selector;

    private void FillSampleData()
    {
        string[] fruit = ["Apple", "Apricot", "Avocado", "Banana", "Blueberry", "Cherry"];

        Completion.ItemsSource = fruit;
        Combo.ItemsSource = fruit;
        EditableCombo.ItemsSource = fruit;
        List.ItemsSource = fruit;
        Items.ItemsSource = new[] { "ItemsControl entry one", "ItemsControl entry two" };
        Slides.ItemsSource = new[] { "Carousel page 1", "Carousel page 2", "Carousel page 3" };

        Table.ItemsSource = new[]
        {
            new GalleryRow("Ada Lovelace", "Analyst", 12),
            new GalleryRow("Alan Turing", "Cryptographer", 7),
            new GalleryRow("Grace Hopper", "Compiler author", 21),
            new GalleryRow("Edsger Dijkstra", "Theorist", 3),
        };

        Tree.ItemsSource = new[]
        {
            new TreeNode("Controls", [new TreeNode("Button"), new TreeNode("TextBox"), new TreeNode("ComboBox")]),
            new TreeNode("Containers", [new TreeNode("Expander"), new TreeNode("SplitView")]),
        };
    }

    /// <summary>Minimal hierarchical node so the TreeView shows more than one level.</summary>
    public sealed class TreeNode(string title, IReadOnlyList<TreeNode>? children = null)
    {
        public string Title { get; } = title;
        public IReadOnlyList<TreeNode> Children { get; } = children ?? [];
        public override string ToString() => Title;
    }
}
