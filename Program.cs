using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.CommandLineUtils;

namespace BreachImporter
{
    class Program
    {
        private const int BatchSize = 500;
        static void Main(string[] args)
        {
            var app = new CommandLineApplication
            {
                Name = "BreachImporter",
                Description = "A dotnet core console application that reads multiple text files under a certain directory (from the breach compilation for example) and imports the emails and passwords int MySQL database. The contents of the files should be in the format USERNAME:PASSWORD, each individual pair on a new line."
            };

            var breachCompilationDataPath = app.Option("-d|--path",
                "The path to the data folder of the breach compilation", CommandOptionType.SingleValue);
            var mysqlDatabase = app.Option("-d|--database",
                "The name of the MySql database to import to", CommandOptionType.SingleValue);
            var mysqlTable = app.Option("-t|--table",
                "The name of the MySql table to import data to (the table has to have two string columns named user and pass)", CommandOptionType.SingleValue);
            var mysqlUsername = app.Option("-u|--username",
                "The MySql username to use for the import process", CommandOptionType.SingleValue);
            var mysqlPassword = app.Option("-p|--password",
                "The MySql password to use for the import process", CommandOptionType.SingleValue);

            app.HelpOption("-?|-h|--help");
            app.VersionOption("-v|--version", () =>
                $"Version {Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion}");

            app.OnExecute(() =>
            {
                var nullOptions = app.Options.Where(o => !o.HasValue() && o.LongName != "password" && o.OptionType == CommandOptionType.SingleValue).ToList();
                if (nullOptions.Any())
                {
                    foreach (var item in nullOptions)
                        Console.WriteLine($"--{item.LongName} is mandatory");

                    app.ShowHint();
                }
                else
                {
                    var directory = new DirectoryInfo(breachCompilationDataPath.Value());
                    var batches = GetBatches(directory);
                    long counter = 0;
                    foreach (var item in batches)
                    {
                        var query = PrepareQueryForBatch(mysqlTable, item);
                        var passwordString = mysqlPassword.HasValue() ? $"-p {mysqlPassword.Value()}" : string.Empty;
                        var command = $"mysql -u {mysqlUsername.Value()} {passwordString} {mysqlDatabase.Value()} -e \"\"{query}\"\"";

                        ExecuteBashCommand(command);
                        Console.SetCursorPosition(0, 1);
                        Console.WriteLine(FormatNumber(counter += BatchSize));
                    }
                }

                return 0;
            });

            try
            {
                app.Execute(args);
            }
            catch (CommandParsingException ex)
            {
                Console.WriteLine(ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unable to execute application: {0}", ex.StackTrace);
            }
        }

        static string FormatNumber(long num)
        {
            if (num >= 100000000)
            {
                return (num / 1000000D).ToString("0.#M");
            }
            if (num >= 1000000)
            {
                return (num / 1000000D).ToString("0.##M");
            }
            if (num >= 100000)
            {
                return (num / 1000D).ToString("0.#k");
            }
            if (num >= 10000)
            {
                return (num / 1000D).ToString("0.##k");
            }

            return num.ToString("#,0");
        }

        private static string PrepareQueryForBatch(CommandOption mysqlTable, IEnumerable<KeyValuePair<string, string>> batch)
        {
            var values = string.Join(",", batch.Select(r => $"('{r.Key}','{r.Value}')"));
            return $"INSERT INTO {mysqlTable.Value()}(user, pass) VALUES {values};";
        }

        private static IEnumerable<IEnumerable<KeyValuePair<string, string>>> GetBatches(DirectoryInfo directory)
        {
            var records = ReadAllFilesAndParse(directory);

            using (var enumerator = records.GetEnumerator())
            {
                var batch = new List<KeyValuePair<string, string>>();
                while (enumerator.MoveNext())
                {
                    var item = enumerator.Current;
                    batch.Add(item);

                    if (batch.Count == BatchSize)
                    {
                        yield return batch;
                        batch = new List<KeyValuePair<string, string>>();
                    }
                }

                yield return batch;
            }
        }

        private static IEnumerable<KeyValuePair<string, string>> ReadAllFilesAndParse(DirectoryInfo directory)
        {
            foreach (var fileInfo in directory.GetFiles("*", SearchOption.AllDirectories))
            {
                using (var file = new StreamReader(fileInfo.FullName))
                {
                    string line;
                    while ((line = file.ReadLine()) != null)
                    {
                        var chunks = line.Split(new[] { ":", ";", "|" }, 2, StringSplitOptions.RemoveEmptyEntries);
                        if (chunks.Length == 2)
                        {
                            var user = Escape(chunks[0]);
                            var pass = Escape(chunks[1]);

                            if (IsValidEmail(user))
                            {
                                yield return new KeyValuePair<string, string>(user, pass);
                            }
                        }
                    }
                }
            }
        }

        private static bool IsValidEmail(string email)
        {
            try
            {
                var address = new System.Net.Mail.MailAddress(email);
                return address.Address == email && Regex.IsMatch(email, @"\A(?:[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*@(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?)\Z", RegexOptions.IgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static string Escape(string data)
        {
            return Regex.Replace(data, @"[\x00'""\b\n\r\t\cZ\\%_]",
                delegate (Match match)
                {
                    string v = match.Value;
                    switch (v)
                    {
                        case "\x00":            // ASCII NUL (0x00) character
                            return "\\0";
                        case "\b":              // BACKSPACE character
                            return "\\b";
                        case "\n":              // NEWLINE (linefeed) character
                            return "\\n";
                        case "\r":              // CARRIAGE RETURN character
                            return "\\r";
                        case "\t":              // TAB
                            return "\\t";
                        case "\u001A":          // Ctrl-Z
                            return "\\Z";
                        default:
                            return "\\" + v;
                    }
                });
        }

        private static string ExecuteBashCommand(string command)
        {
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = "-c \"" + command + "\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            proc.Start();
            proc.WaitForExit();

            return proc.StandardOutput.ReadToEnd();
        }
    }
}
