using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DevSnippetManager.Services.ProjectScan;

public static class ProjectLocator
{
    private static readonly HashSet<string> SkippedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", "node_modules", ".git", ".vs", ".idea", "dist", "build", "out",
        "packages", "target", "vendor", "__pycache__", ".venv", "venv", "monaco"
    };

    private static readonly string[] ProjectMarkers =
    {
        ".git", "package.json", "pyproject.toml", "go.mod", "Cargo.toml"
    };

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".java", ".js", ".jsx", ".mjs", ".cjs", ".ts", ".tsx", ".c", ".h",
        ".cpp", ".hpp", ".cc", ".cxx", ".go", ".rs", ".php", ".kt", ".kts", ".swift",
        ".py", ".rb", ".ps1", ".psm1", ".sh", ".bash", ".sql"
    };

    public static string NormalizePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path).Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        var root = Path.GetPathRoot(fullPath);
        if (root is not null && string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase))
            return root.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

        return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    public static bool PathEquals(string a, string b)
    {
        return string.Equals(NormalizePath(a), NormalizePath(b), StringComparison.OrdinalIgnoreCase);
    }

    public static string FindProjectRoot(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var normalized = NormalizePath(filePath);
        var directory = Directory.Exists(normalized)
            ? normalized
            : Path.GetDirectoryName(normalized) ?? normalized;

        while (!string.IsNullOrEmpty(directory))
        {
            if (ContainsProjectMarker(directory))
                return NormalizePath(directory);

            var parent = Directory.GetParent(directory)?.FullName;
            if (parent is null || PathEquals(parent, directory))
                break;
            directory = parent;
        }

        return NormalizePath(Directory.Exists(normalized)
            ? normalized
            : Path.GetDirectoryName(normalized) ?? normalized);
    }

    public static IReadOnlyList<string> EnumerateSourceFiles(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);

        var root = NormalizePath(folder);
        if (!Directory.Exists(root))
            return Array.Empty<string>();

        var files = new List<string>();
        EnumerateDirectory(root, files);
        files.Sort(StringComparer.OrdinalIgnoreCase);
        return files;
    }

    public static bool ShouldSkipFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var normalized = NormalizePath(path);
        var fileName = Path.GetFileName(normalized);
        if (fileName.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".min.js", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains(".generated.", StringComparison.OrdinalIgnoreCase))
            return true;

        return normalized.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(part => string.Equals(part, "Migrations", StringComparison.OrdinalIgnoreCase));
    }

    public static string? LanguageFromPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".cs" => "C#",
            ".java" => "Java",
            ".js" or ".jsx" or ".mjs" or ".cjs" => "JavaScript",
            ".ts" or ".tsx" => "TypeScript",
            ".c" or ".h" => "C",
            ".cpp" or ".hpp" or ".cc" or ".cxx" => "C++",
            ".go" => "Go",
            ".rs" => "Rust",
            ".php" => "PHP",
            ".kt" or ".kts" => "Kotlin",
            ".swift" => "Swift",
            ".py" => "Python",
            ".rb" => "Ruby",
            ".ps1" or ".psm1" => "PowerShell",
            ".sh" or ".bash" => "Bash",
            ".sql" => "SQL",
            _ => null
        };
    }

    public static string ProjectFolderName(string root, IReadOnlyCollection<(string Root, string Folder)> existing)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentNullException.ThrowIfNull(existing);

        var normalizedRoot = NormalizePath(root);
        var name = new DirectoryInfo(normalizedRoot).Name;
        var hasConflict = existing.Any(item =>
            string.Equals(item.Folder, name, StringComparison.OrdinalIgnoreCase)
            && !PathEquals(item.Root, normalizedRoot));

        if (!hasConflict)
            return name;

        var parentName = Directory.GetParent(normalizedRoot)?.Name;
        return string.IsNullOrWhiteSpace(parentName) ? name : $"{name} ({parentName})";
    }

    private static bool ContainsProjectMarker(string directory)
    {
        try
        {
            if (Directory.Exists(Path.Combine(directory, ".git"))
                || File.Exists(Path.Combine(directory, ".git")))
                return true;

            if (Directory.EnumerateFiles(directory, "*.sln", SearchOption.TopDirectoryOnly).Any()
                || Directory.EnumerateFiles(directory, "*.slnx", SearchOption.TopDirectoryOnly).Any()
                || Directory.EnumerateFiles(directory, "*.csproj", SearchOption.TopDirectoryOnly).Any())
                return true;

            return ProjectMarkers.Skip(1).Any(marker => File.Exists(Path.Combine(directory, marker)));
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void EnumerateDirectory(string directory, List<string> files)
    {
        IEnumerable<string> childDirectories;
        try
        {
            childDirectories = Directory.EnumerateDirectories(directory)
                .Where(child => !SkippedDirectoryNames.Contains(Path.GetFileName(child)))
                .OrderBy(child => child, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (IOException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        IEnumerable<string> childFiles;
        try
        {
            childFiles = Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly).ToArray();
        }
        catch (IOException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        foreach (var file in childFiles.OrderBy(file => file, StringComparer.OrdinalIgnoreCase))
        {
            if (!ShouldSkipFile(file) && LanguageFromPath(file) is not null)
                files.Add(NormalizePath(file));
        }

        foreach (var childDirectory in childDirectories)
            EnumerateDirectory(childDirectory, files);
    }
}
