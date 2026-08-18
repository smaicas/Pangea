; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category               | Severity | Notes
--------|------------------------|----------|-------------------------------------------------------------
PGD001  | CdCSharp.Pangea.Data   | Warning  | The context is registered with no database engine
PGD002  | CdCSharp.Pangea.Data   | Warning  | The DbContext is resolved from the container
PGD003  | CdCSharp.Pangea.Data   | Warning  | SaveChanges inside WriteAsync
