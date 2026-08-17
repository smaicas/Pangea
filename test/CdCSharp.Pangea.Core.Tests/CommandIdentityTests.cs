using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Core.Tests.Infrastructure;

namespace CdCSharp.Pangea.Core.Tests;

/// <summary>
/// A command exposed as an expression-bodied property is read once by the binding and many times by
/// everything else. If each read built a new command, the instance the UI holds would never hear
/// <c>RaiseCanExecuteChanged</c> - which is the call the generator emits.
/// </summary>
public class CommandIdentityTests
{
    private sealed class Subject : ViewModelBase
    {
        private bool _allowed;

        public Subject(IServiceProvider services) : base(services) { }

        public bool Allowed
        {
            get => _allowed;
            set
            {
                if (SetProperty(ref _allowed, value))
                {
                    // Exactly what the source generator emits for a command whose CanExecute
                    // reads this property.
                    SaveCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private void Save() { }

        public RelayCommand SaveCommand => CreateCommand(Save, () => Allowed);

        public RelayCommand OtherCommand => CreateCommand(Save);
    }

    /// <summary>
    /// Commands built in a constructor all share one caller member name, so the cache has to tell
    /// them apart by their bodies or they would collapse into a single command.
    /// </summary>
    private sealed class ConstructorStyle : ViewModelBase
    {
        public ConstructorStyle(IServiceProvider services) : base(services)
        {
            FirstCommand = CreateCommand(First);
            SecondCommand = CreateCommand(Second);
            ThirdCommand = CreateCommand(() => { });
            FourthCommand = CreateCommand(() => { });
        }

        public RelayCommand FirstCommand { get; }

        public RelayCommand SecondCommand { get; }

        public RelayCommand ThirdCommand { get; }

        public RelayCommand FourthCommand { get; }

        private void First() { }

        private void Second() { }
    }

    /// <summary>Two commands sharing a body: nothing says they may be merged into one.</summary>
    private sealed class SharedBodyStyle : ViewModelBase
    {
        public SharedBodyStyle(IServiceProvider services) : base(services)
        {
            RefreshCommand = CreateCommand(Refresh);
            ReloadCommand = CreateCommand(Refresh);
        }

        public RelayCommand RefreshCommand { get; }

        public RelayCommand ReloadCommand { get; }

        private void Refresh() { }
    }

    [Fact]
    public void TwoConstructorCommandsSharingABody_AreStillTwoCommands()
    {
        SharedBodyStyle subject = new(new StubServices(new RelayCommandFactory(new FakeUIDispatcher())));

        Assert.NotSame(subject.RefreshCommand, subject.ReloadCommand);
    }

    /// <summary>
    /// The constructor style never had the bug - the field holds one instance - but the cache must
    /// not have broken it either.
    /// </summary>
    [Fact]
    public void AConstructorAssignedCommand_HearsCanExecuteChanged()
    {
        ConstructorStyle subject = new(new StubServices(new RelayCommandFactory(new FakeUIDispatcher())));

        RelayCommand bound = subject.FirstCommand;
        int notifications = 0;
        bound.CanExecuteChanged += (_, _) => notifications++;

        subject.FirstCommand.RaiseCanExecuteChanged();

        Assert.Equal(1, notifications);
    }

    [Fact]
    public void CommandsBuiltInAConstructor_StayDistinct()
    {
        ConstructorStyle subject = new(new StubServices(new RelayCommandFactory(new FakeUIDispatcher())));

        RelayCommand[] commands =
            [subject.FirstCommand, subject.SecondCommand, subject.ThirdCommand, subject.FourthCommand];

        Assert.Equal(4, commands.Distinct().Count());
    }

    private sealed class StubServices(IRelayCommandFactory factory) : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(IRelayCommandFactory) ? factory : null;
    }

    private static Subject Create()
    {
        return new Subject(new StubServices(new RelayCommandFactory(new FakeUIDispatcher())));
    }

    [Fact]
    public void ReadingACommandPropertyTwice_GivesTheSameCommand()
    {
        Subject subject = Create();

        Assert.Same(subject.SaveCommand, subject.SaveCommand);
    }

    [Fact]
    public void DifferentCommandProperties_AreDifferentCommands()
    {
        Subject subject = Create();

        Assert.NotSame((object)subject.SaveCommand, subject.OtherCommand);
    }

    /// <summary>
    /// The failure the user sees: a button bound once, and a CanExecute that never refreshes.
    /// </summary>
    [Fact]
    public void TheInstanceTheUIHolds_HearsCanExecuteChanged()
    {
        Subject subject = Create();

        RelayCommand bound = subject.SaveCommand;   // what the binding captured
        int notifications = 0;
        bound.CanExecuteChanged += (_, _) => notifications++;

        Assert.False(bound.CanExecute(null));

        subject.Allowed = true;

        Assert.True(notifications > 0);
        Assert.True(bound.CanExecute(null));
    }
}
