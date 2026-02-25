using System.Text;
using Cm6206AssetGenerator;

// Usage:
//   dotnet run -c Release --project tools/Cm6206AssetGenerator -- --out assets/generated
//
// Outputs:
//   assets/generated/{png,svg,9slice}/(dark|light)/...

try
{
    var argsList = args.ToList();
    var outDir = Arg.GetValue(argsList, "--out") ?? Path.Combine("assets", "generated");
    var themeArg = (Arg.GetValue(argsList, "--theme") ?? "all").Trim().ToLowerInvariant();

    var themes = themeArg switch
    {
        "dark" => new[] { ThemeVariant.Dark },
        "light" => new[] { ThemeVariant.Light },
        "all" => new[] { ThemeVariant.Dark, ThemeVariant.Light },
        _ => throw new ArgumentException("--theme must be one of: dark, light, all")
    };

    Directory.CreateDirectory(outDir);

    var generator = new AssetPackGenerator(outDir);

    foreach (var theme in themes)
    {
        Console.WriteLine($"Generating assets: {theme} → {outDir}");
        generator.GenerateAll(theme);
    }

    Console.WriteLine("Done.");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}

internal static class Arg
{
    public static string? GetValue(List<string> args, string key)
    {
        var i = args.FindIndex(a => string.Equals(a, key, StringComparison.OrdinalIgnoreCase));
        if (i < 0) return null;
        if (i + 1 >= args.Count) return string.Empty;
        return args[i + 1];
    }
}
