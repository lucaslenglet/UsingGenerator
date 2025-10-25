namespace UsingGenerator.Sample;

public static class Greeter
{
    /// <summary>
    /// Uses <c>System.Linq</c> without importing it: the import comes from the applied bundles.
    /// </summary>
    public static string Build(params string[] parts) =>
        string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
}
