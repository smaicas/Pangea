; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category              | Severity | Notes
--------|-----------------------|----------|-----------------------------------------------------------
PGA001  | CdCSharp.Pangea.Apps  | Error    | A handled request has no [AppMessage] id
PGA002  | CdCSharp.Pangea.Apps  | Error    | Two handlers answer the same message id
PGA003  | CdCSharp.Pangea.Apps  | Error    | A handler is private and cannot be registered
