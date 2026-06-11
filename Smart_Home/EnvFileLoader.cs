using System;
using System.IO;

namespace Smart_Home
{
    /// <summary>
    /// Minimal, dependency-free loader for a <c>.env</c> file.
    /// .NET's <c>AddEnvironmentVariables()</c> only reads process environment variables, not a .env file,
    /// so this populates the process environment from .env before configuration is built.
    /// Real, pre-existing environment variables always win — values from .env never overwrite them.
    /// </summary>
    public static class EnvFileLoader
    {
        /// <summary>
        /// Locates the nearest <c>.env</c> file (output directory first, then walking up the directory tree
        /// to cover the dev case where .env lives in the project folder) and loads its key/value pairs.
        /// </summary>
        public static void Load(string fileName = ".env")
        {
            var path = FindEnvFile(fileName);
            if (path is null)
            {
                return;
            }

            foreach (var rawLine in File.ReadLines(path))
            {
                var line = rawLine.Trim();

                // Skip blank lines and comments.
                if (line.Length == 0 || line.StartsWith('#'))
                {
                    continue;
                }

                var separatorIndex = line.IndexOf('=');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                var key = line[..separatorIndex].Trim();
                var value = line[(separatorIndex + 1)..].Trim();
                value = StripSurroundingQuotes(value);

                if (key.Length == 0)
                {
                    continue;
                }

                // Real environment variables take precedence over .env values.
                if (Environment.GetEnvironmentVariable(key) is not null)
                {
                    continue;
                }

                Environment.SetEnvironmentVariable(key, value);
            }
        }

        private static string? FindEnvFile(string fileName)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            return null;
        }

        private static string StripSurroundingQuotes(string value)
        {
            if (value.Length >= 2 &&
                ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
            {
                return value[1..^1];
            }

            return value;
        }
    }
}
