[assembly: Demo.Bundles.DemoUsings]

int[] numbers = [1, 2, 3, 4];

// Where -> System.Linq, WriteLine -> static System.Console. Neither is imported here.
WriteLine(numbers.Where(n => n % 2 == 0).Count());
