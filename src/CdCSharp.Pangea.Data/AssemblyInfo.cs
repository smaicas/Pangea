using System.Runtime.CompilerServices;

// The runtime, the accessor and the initializer are implementation: the application sees the
// interfaces. The tests drive the implementations directly rather than through a running
// application, which is the only way to exercise a failed migration.
[assembly: InternalsVisibleTo("CdCSharp.Pangea.Data.Tests")]
