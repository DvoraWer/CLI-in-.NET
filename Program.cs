using System;
using System.CommandLine;
using System.Collections.Generic;
using System.IO;
using System.Linq;

string[] codeExtensions =
{
    ".cs", ".csx", ".vb", ".fs", ".fsx",
    ".c", ".h", ".cc", ".cpp", ".cxx",
    ".hh", ".hpp", ".hxx", ".inl", ".asm", ".s",
    ".java", ".kt", ".kts", ".scala", ".groovy",
    ".js", ".mjs", ".cjs", ".jsx",
    ".ts", ".mts", ".cts", ".tsx",
    ".py", ".pyw", ".pyx", ".pxd", ".pxi",
    ".html", ".htm", ".css", ".scss", ".sass",
    ".less", ".vue", ".svelte",
    ".php", ".phtml", ".rb", ".rake",
    ".go", ".rs", ".swift", ".m", ".mm",
    ".dart", ".lua", ".r",
    ".hs", ".lhs", ".clj", ".cljs", ".cljc",
    ".ex", ".exs", ".erl", ".hrl", ".elm",
    ".sh", ".bash", ".zsh", ".ps1", ".psm1",
    ".bat", ".cmd", ".sql", ".plsql",
    ".razor", ".cshtml", ".vbhtml",
    ".pl", ".pm", ".sol", ".vy",
    ".apex", ".trigger", ".cls"
};

string[] excludedDirectories =
{
    "bin", "obj", "Debug", "Release", ".git", "node_modules"
};

var bundleCommand = new Command("bundle", "Bundle code files to a single file");
var createRspCommand = new Command("create-rsp", "Create a response file for the bundle command");

// Options

var languageOption = new Option<string[]>("--language")
{
    Required = true,
    Description = "Languages to add"
};
languageOption.Aliases.Add("-l");

var outputOption = new Option<string>("--output")
{
    Required = true,
    Description = "Output file path"
};
outputOption.Aliases.Add("-o");

var noteOption = new Option<bool>("--note")
{
    Description = "Include source file name and relative path"
};
noteOption.Aliases.Add("-n");

var removeEmptyLinesOption = new Option<bool>("--remove-empty-lines")
{
    Description = "Remove empty lines from source files"
};
removeEmptyLinesOption.Aliases.Add("-r");

var authorOption = new Option<string>("--author")
{
    Description = "Add the bundle's author name"
};
authorOption.Aliases.Add("-a");

var sortOption = new Option<string>("--sort")
{
    DefaultValueFactory = _ => "name",
    Description = "Sort files by name or type"
};
sortOption.Aliases.Add("-s");

// Register options exactly once

bundleCommand.Options.Add(languageOption);
bundleCommand.Options.Add(outputOption);
bundleCommand.Options.Add(noteOption);
bundleCommand.Options.Add(removeEmptyLinesOption);
bundleCommand.Options.Add(authorOption);
bundleCommand.Options.Add(sortOption);

// Bundle command

bundleCommand.SetAction(parseResult =>
{
    var languages = parseResult.GetValue(languageOption);
    var outputPath = parseResult.GetValue(outputOption);
    bool includeNote = parseResult.GetValue(noteOption);
    bool removeEmptyLines = parseResult.GetValue(removeEmptyLinesOption);
    var author = parseResult.GetValue(authorOption);
    var sort = parseResult.GetValue(sortOption);

    if (languages == null || languages.Length == 0)
    {
        Console.Error.WriteLine("Please specify at least one language.");
        return 1;
    }

    string[] requestedLanguages = languages
        .Select(language => language.Trim())
        .ToArray();

    bool includeAll = requestedLanguages.Contains(
        "all", StringComparer.OrdinalIgnoreCase);

    var supportedLanguages = codeExtensions
        .Select(extension => extension.TrimStart('.'))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    if (!includeAll)
    {
        var invalidLanguages = requestedLanguages
            .Where(language => !supportedLanguages.Contains(language))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (invalidLanguages.Count > 0)
        {
            Console.Error.WriteLine(
                $"Unsupported language(s): {string.Join(", ", invalidLanguages)}");
            return 1;
        }
    }

    if (!sort.Equals("name", StringComparison.OrdinalIgnoreCase) &&
        !sort.Equals("type", StringComparison.OrdinalIgnoreCase))
    {
        Console.Error.WriteLine("Invalid sort value. Use 'name' or 'type'.");
        return 1;
    }

    string fullOutputPath = Path.GetFullPath(outputPath);
    string folderPath = Directory.GetCurrentDirectory();

    List<string> files = GetSourceFiles(
        folderPath, requestedLanguages, includeAll);

    files = files
        .Where(file => !Path.GetFullPath(file).Equals(
            fullOutputPath, StringComparison.OrdinalIgnoreCase))
        .ToList();

    if (sort.Equals("type", StringComparison.OrdinalIgnoreCase))
    {
        files = files
            .OrderBy(path => Path.GetExtension(path),
                StringComparer.OrdinalIgnoreCase)
            .ThenBy(path => Path.GetFileName(path),
                StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
    else
    {
        files = files
            .OrderBy(path => Path.GetFileName(path),
                StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    try
    {
        using (StreamWriter writer = new StreamWriter(fullOutputPath))
        {
            if (!string.IsNullOrWhiteSpace(author))
            {
                writer.WriteLine($"// Author: {author}");
                writer.WriteLine();
            }

            foreach (var file in files)
            {
                if (includeNote)
                {
                    string relativePath = Path.GetRelativePath(folderPath, file);
                    writer.WriteLine(
                        $"// Source: {Path.GetFileName(file)} | Path: {relativePath}");
                }

                string content = File.ReadAllText(file);

                if (removeEmptyLines)
                {
                    content = string.Join(
                        Environment.NewLine,
                        content.Split(
                            new[] { "\r\n", "\n", "\r" },
                            StringSplitOptions.None)
                        .Where(line => !string.IsNullOrWhiteSpace(line)));
                }

                writer.WriteLine(content);
                writer.WriteLine();
            }
        }

        Console.WriteLine($"Bundle created successfully: {fullOutputPath}");
        return 0;
    }
    catch (IOException ex)
    {
        Console.Error.WriteLine($"Error creating bundle: {ex.Message}");
        return 1;
    }
    catch (UnauthorizedAccessException ex)
    {
        Console.Error.WriteLine($"Access denied: {ex.Message}");
        return 1;
    }
});

// Create response file

createRspCommand.SetAction(_ =>
{
    Console.Write("Languages (e.g. cs or all): ");
    string languagesInput = Console.ReadLine() ?? "";

    Console.Write("Output file path: ");
    string output = Console.ReadLine() ?? "";

    Console.Write("Include source notes? (y/n): ");
    string note = Console.ReadLine() ?? "n";

    Console.Write("Remove empty lines? (y/n): ");
    string removeEmpty = Console.ReadLine() ?? "n";

    Console.Write("Author name (optional): ");
    string author = Console.ReadLine() ?? "";

    Console.Write("Sort by name or type? (name/type): ");
    string sort = Console.ReadLine() ?? "name";

    Console.Write("Response file name: ");
    string responseFile = Console.ReadLine() ?? "";

    if (string.IsNullOrWhiteSpace(languagesInput) ||
        string.IsNullOrWhiteSpace(output) ||
        string.IsNullOrWhiteSpace(responseFile))
    {
        Console.Error.WriteLine(
            "Languages, output path and response file name are required.");
        return 1;
    }

    if (!sort.Equals("name", StringComparison.OrdinalIgnoreCase) &&
        !sort.Equals("type", StringComparison.OrdinalIgnoreCase))
    {
        Console.Error.WriteLine("Sort must be 'name' or 'type'.");
        return 1;
    }

    var arguments = new List<string>
    {
        "bundle",
        "--language",
        languagesInput.Trim(),
        "--output",
        output.Trim(),
        "--sort",
        sort.Trim()
    };

    if (note.Trim().Equals("y", StringComparison.OrdinalIgnoreCase))
        arguments.Add("--note");

    if (removeEmpty.Trim().Equals("y", StringComparison.OrdinalIgnoreCase))
        arguments.Add("--remove-empty-lines");

    if (!string.IsNullOrWhiteSpace(author))
    {
        arguments.Add("--author");
        arguments.Add(author.Trim());
    }

    string content = string.Join(
        Environment.NewLine,
        arguments.Select(arg =>
            arg.Contains(' ') || arg.Contains('"')
                ? "\"" + arg.Replace("\"", "\\\"") + "\""
                : arg));

    try
    {
        File.WriteAllText(responseFile.Trim(), content);
        Console.WriteLine(
            $"Response file created: {Path.GetFullPath(responseFile.Trim())}");
        return 0;
    }
    catch (IOException ex)
    {
        Console.Error.WriteLine($"Could not create response file: {ex.Message}");
        return 1;
    }
    catch (UnauthorizedAccessException ex)
    {
        Console.Error.WriteLine($"Access denied: {ex.Message}");
        return 1;
    }
});

// File collection

List<string> GetSourceFiles(string folderPath, string[] langs, bool isAll)
{
    List<string> res = new List<string>();

    foreach (var path in Directory.GetFiles(folderPath))
    {
        if (IsSupportedFile(path, langs, isAll))
            res.Add(path);
    }

    foreach (var directory in Directory.GetDirectories(folderPath))
    {
        string directoryName = Path.GetFileName(directory);

        if (excludedDirectories.Contains(
            directoryName, StringComparer.OrdinalIgnoreCase))
            continue;

        res.AddRange(GetSourceFiles(directory, langs, isAll));
    }

    return res
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();
}

bool IsSupportedFile(string filePath, string[] langs, bool isAll)
{
    string extension = Path.GetExtension(filePath);

    if (isAll)
    {
        return codeExtensions.Contains(
            extension, StringComparer.OrdinalIgnoreCase);
    }

    foreach (var lang in langs)
    {
        if (("." + lang).Equals(
            extension, StringComparison.OrdinalIgnoreCase))
            return true;
    }

    return false;
}

// Root command: each subcommand is registered once

var rootCommand = new RootCommand("A root command for files bundler CLI");
rootCommand.Subcommands.Add(bundleCommand);
rootCommand.Subcommands.Add(createRspCommand);
Console.WriteLine("Arguments: " + string.Join(" | ", args));
Console.WriteLine("Root commands: " +
    string.Join(", ", rootCommand.Subcommands.Select(c => c.Name)));
return rootCommand.Parse(args).Invoke();