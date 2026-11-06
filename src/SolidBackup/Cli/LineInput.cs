using System.Text;

using Spectre.Console;

namespace SolidBackup.Cli;

/// <summary>
/// Reads a line of text key by key (E8-3, E8-4), so Esc can cancel it: Spectre's text prompt cannot be left with Esc,
/// and it reads its default value as markup.
/// </summary>
public static class LineInput
{
    /// <summary>A line typed after <paramref name="prompt"/>, or null for Esc.</summary>
    public static string? Ask(IAnsiConsole console, string prompt)
    {
        console.Write(prompt);
        StringBuilder line = new();
        while (true)
        {
            if (console.Input.ReadKey(intercept: true) is not { } key)
            {
                continue;
            }

            switch (key.Key)
            {
                case ConsoleKey.Escape:
                    console.WriteLine();
                    return null;
                case ConsoleKey.Enter:
                    console.WriteLine();
                    return line.ToString();
                case ConsoleKey.Backspace when line.Length > 0:
                    line.Length--;
                    console.Write("\b \b");
                    break;
                default:
                    if (!char.IsControl(key.KeyChar))
                    {
                        line.Append(key.KeyChar);
                        console.Write(key.KeyChar.ToString());
                    }

                    break;
            }
        }
    }
}
