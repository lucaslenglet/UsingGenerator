using System.ComponentModel;

namespace System.Runtime.CompilerServices;

/// <summary>
/// Required by the compiler to allow init-only setters when targeting netstandard2.0.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
internal static class IsExternalInit;
