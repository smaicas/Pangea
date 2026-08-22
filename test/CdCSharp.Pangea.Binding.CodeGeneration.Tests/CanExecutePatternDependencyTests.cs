using CdCSharp.Pangea.Core.Base;
using System.Reflection;

namespace CdCSharp.Pangea.Binding.CodeGeneration.Tests;

/// <summary>
/// A CanExecute lambda is analysed by walking the expression, and the walk is a switch over the
/// shapes it has been taught. Anything else used to fall through it silently, so the property the
/// predicate really depends on was never registered and the command was never told to re-evaluate.
/// </summary>
/// <remarks>
/// Found in an application: <c>() => IsOwner &amp;&amp; !IsBusy &amp;&amp; SelectedMember is { IsOwner: false }</c>
/// registered [IsOwner, IsBusy] and not SelectedMember, so selecting a member left the button dead.
/// The pattern was one shape of several - a null-conditional read and a switch expression were just
/// as invisible - which is why the switch now ends in a fallback rather than in nothing.
/// </remarks>
public class CanExecutePatternDependencyTests
{
    private const string GroupViewModel = """
        using CdCSharp.Pangea.Binding.Attributes;
        using CdCSharp.Pangea.Core.Base;

        namespace Sample;

        public class GroupMember
        {
            public bool IsOwner { get; set; }
        }

        public partial class GroupSettingsViewModel : ViewModelBase
        {
            public GroupSettingsViewModel(IServiceProvider sp) : base(sp) { }

            [Binding] private bool _isOwner;
            [Binding] private bool _isBusy;
            [Binding] private int _mode;
            [Binding] private string _groupName = string.Empty;
            [Binding] private GroupMember? _selectedMember;

            // Property pattern: the shape that was reported.
            public RelayCommand TransferOwnershipCommand =>
                CreateCommand(Noop, () => IsOwner && !IsBusy && SelectedMember is { IsOwner: false });

            // Constant pattern.
            public RelayCommand ClearSelectionCommand =>
                CreateCommand(Noop, () => SelectedMember is not null);

            // Null-conditional read.
            public RelayCommand PromoteCommand =>
                CreateCommand(Noop, () => SelectedMember?.IsOwner == false);

            // Switch expression: no case names it, so it exercises the fallback.
            public RelayCommand ApplyCommand =>
                CreateCommand(Noop, () => Mode switch { 0 => IsOwner, _ => false });

            private void Noop() { }
        }
        """;

    [Fact]
    public void PropertyPattern_NotifiesTheCommandWhenTheMatchedPropertyChanges()
    {
        object viewModel = CreateViewModel();
        SetProperty(viewModel, "IsOwner", true);

        RelayCommand bound = Command(viewModel, "TransferOwnershipCommand");
        int raised = 0;
        bound.CanExecuteChanged += (_, _) => raised++;

        Assert.False(bound.CanExecute(null));

        SetProperty(viewModel, "SelectedMember", NewMember(viewModel));

        Assert.True(raised >= 1, $"Selecting a member notified the command {raised} time(s).");
        Assert.True(bound.CanExecute(null));
    }

    [Fact]
    public void ConstantPattern_NotifiesTheCommandWhenTheTestedPropertyChanges()
    {
        object viewModel = CreateViewModel();

        RelayCommand bound = Command(viewModel, "ClearSelectionCommand");
        int raised = 0;
        bound.CanExecuteChanged += (_, _) => raised++;

        SetProperty(viewModel, "SelectedMember", NewMember(viewModel));

        Assert.True(raised >= 1, $"The command was notified {raised} time(s).");
        Assert.True(bound.CanExecute(null));
    }

    [Fact]
    public void NullConditionalRead_NotifiesTheCommandWhenTheSubjectChanges()
    {
        object viewModel = CreateViewModel();

        RelayCommand bound = Command(viewModel, "PromoteCommand");
        int raised = 0;
        bound.CanExecuteChanged += (_, _) => raised++;

        SetProperty(viewModel, "SelectedMember", NewMember(viewModel));

        Assert.True(raised >= 1, $"The command was notified {raised} time(s).");
        Assert.True(bound.CanExecute(null));
    }

    [Fact]
    public void SwitchExpression_NotifiesTheCommandForEveryPropertyItReads()
    {
        object viewModel = CreateViewModel();

        RelayCommand bound = Command(viewModel, "ApplyCommand");
        int fromSubject = 0;
        bound.CanExecuteChanged += (_, _) => fromSubject++;

        SetProperty(viewModel, "Mode", 1);
        Assert.True(fromSubject >= 1, "The switch subject notified nothing.");

        int fromArm = 0;
        bound.CanExecuteChanged += (_, _) => fromArm++;

        SetProperty(viewModel, "IsOwner", true);
        Assert.True(fromArm >= 1, "The property read inside an arm notified nothing.");
    }

    /// <summary>
    /// The fallback registers what a predicate reads, not everything the view model declares.
    /// </summary>
    [Fact]
    public void AnUnreadProperty_DoesNotNotifyTheCommand()
    {
        object viewModel = CreateViewModel();

        RelayCommand bound = Command(viewModel, "TransferOwnershipCommand");
        int raised = 0;
        bound.CanExecuteChanged += (_, _) => raised++;

        SetProperty(viewModel, "GroupName", "anything");

        Assert.Equal(0, raised);
    }

    /// <summary>
    /// The analysis report is what a developer reads when a button will not enable, so the
    /// dependency has to be visible there and not only in the emitted setter.
    /// </summary>
    [Fact]
    public void TheAnalysisReport_ListsTheMatchedPropertyAsADependency()
    {
        GeneratorTestHelper.GeneratorResult result = GeneratorTestHelper.Run(GroupViewModel);

        string report = result.Sources
            .Single(source => source.HintName.EndsWith("Analysis.Debug.g.cs", StringComparison.Ordinal))
            .Text;

        string commandSection = report[report.IndexOf("TransferOwnershipCommand:", StringComparison.Ordinal)..];
        string dependencies = commandSection[..commandSection.IndexOf("ClearSelectionCommand", StringComparison.Ordinal)];

        Assert.Contains("SelectedMember", dependencies, StringComparison.Ordinal);
    }

    private static object CreateViewModel()
    {
        Assembly assembly = GeneratorTestHelper.RunAndLoad(GroupViewModel);
        Type type = assembly.GetType("Sample.GroupSettingsViewModel")
                    ?? throw new InvalidOperationException("Sample.GroupSettingsViewModel not found.");

        return Activator.CreateInstance(type, new TestServiceProvider())!;
    }

    private static object NewMember(object viewModel) =>
        Activator.CreateInstance(viewModel.GetType().Assembly.GetType("Sample.GroupMember")!)!;

    private static RelayCommand Command(object viewModel, string name) =>
        (RelayCommand)GetProperty(viewModel, name)!;

    private static void SetProperty(object viewModel, string name, object? value) =>
        viewModel.GetType().GetProperty(name)!.SetValue(viewModel, value);

    private static object? GetProperty(object viewModel, string name) =>
        viewModel.GetType().GetProperty(name)!.GetValue(viewModel);
}
