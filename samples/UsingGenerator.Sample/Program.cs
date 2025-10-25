// The bundle declared by the referenced library, pulled in with a single line.
[assembly: UsingGenerator.Sample.Library.DefaultUsings]

namespace UsingGenerator.Sample;

public static class Program
{
    public static void Main()
    {
        // StringList -> local SampleUsings bundle.
        StringList parts = ["", "Hello,", "World", "!"];

        // Where -> bundled System.Linq. WriteLine -> bundled static System.Console.
        WriteLine(Greeter.Build([.. parts.Where(p => p.Length > 0)]));
    }
}
