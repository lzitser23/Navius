namespace Navius.Motion.Tests;

/// <summary>Locates repo files from the test output directory.</summary>
internal static class RepoPaths
{
    /// <summary>Walk up from the test bin directory to the repo root.</summary>
    public static string Root
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "src", "Navius.Motion", "wwwroot", "navius-motion.js")))
                {
                    return dir.FullName;
                }
                dir = dir.Parent!;
            }
            throw new InvalidOperationException("Could not locate the repo root from " + AppContext.BaseDirectory);
        }
    }

    public static string EngineModule => Path.Combine(Root, "src", "Navius.Motion", "wwwroot", "navius-motion.js");

    public static string CommittedStylesheet => Path.Combine(Root, "src", "Navius.Motion", "wwwroot", "navius-motion.css");
}
