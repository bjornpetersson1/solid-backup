using System.Text;

namespace SolidBackup.Import;

/// <summary>
/// A path as typed, or as the terminal pastes it when a file is dragged in from Finder (E8-4): surrounding quotes are
/// removed (with the shell's <c>'\''</c> for a quote inside single quotes), backslash escapes such as <c>\ </c> are
/// undone, and a leading <c>~</c> is the home folder. Spaces around the path are dropped, but not an escaped one.
/// </summary>
public static class ClipPath
{
    public static string Parse(string typed)
    {
        string path = typed.TrimStart();
        string trimmed = path.TrimEnd();
        if (trimmed.Length >= 2 && trimmed[0] == '\'' && trimmed[^1] == '\'')
        {
            return Home(trimmed[1..^1].Replace("'\\''", "'", StringComparison.Ordinal));
        }

        if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
        {
            return Home(trimmed[1..^1]);
        }

        return Home(Unescape(path));
    }

    // Undoes backslash escapes; unescaped spaces at the end are dropped.
    private static string Unescape(string path)
    {
        StringBuilder unescaped = new(path.Length);
        int kept = 0;
        for (int i = 0; i < path.Length; i++)
        {
            bool escaped = path[i] == '\\' && i + 1 < path.Length;
            if (escaped)
            {
                i++;
            }

            unescaped.Append(path[i]);
            if (escaped || !char.IsWhiteSpace(path[i]))
            {
                kept = unescaped.Length;
            }
        }

        return unescaped.ToString(0, kept);
    }

    private static string Home(string path)
    {
        if (path == "~" || path.StartsWith("~/", StringComparison.Ordinal))
        {
            return Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[1..].TrimStart('/'));
        }

        return path;
    }
}
