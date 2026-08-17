; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|--------------------------|----------|-------------------------------------------------------------
PGB001  | CdCSharp.Pangea.Binding  | Error    | A view model with [Binding] fields must be partial
PGB002  | CdCSharp.Pangea.Binding  | Error    | A view model with [Binding] fields must derive from ViewModelBase
PGB003  | CdCSharp.Pangea.Binding  | Error    | Two [Binding] fields produce the same property
PGB004  | CdCSharp.Pangea.Binding  | Error    | The generated property name is already declared
PGB005  | CdCSharp.Pangea.Binding  | Warning  | [Binding] does not apply to static fields
