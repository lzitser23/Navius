using System.Text;
using Navius.Motion;

// Writes the generated navius-motion.css (see MotionStylesheet). Deterministic:
// running twice always produces byte-identical output. Usage, from the repo root:
//
//   dotnet run --project src/Navius.Motion.CssGen [output-path]
//
// The default output path is the committed stylesheet next to the engine module.

var outputPath = args.Length > 0
    ? args[0]
    : Path.Combine("src", "Navius.Motion", "wwwroot", "navius-motion.css");

var css = MotionStylesheet.Generate();
File.WriteAllText(outputPath, css, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
Console.WriteLine($"Wrote {css.Length} chars to {Path.GetFullPath(outputPath)}");
