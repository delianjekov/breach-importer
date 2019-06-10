using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Extensions.CommandLineUtils;

namespace BreachImporter
{
    class Program
    {
        static void Main(string[] args)
        {
            var app = new CommandLineApplication
            {
                Name = "BreachImporter",
                Description = "A dotnet core console application that reads multiple text files under a certain directory (from the breach compilation for example) and imports the emails and passwords int MySQL database. The contents of the files should be in the format USERNAME:PASSWORD, each individual pair on a new line."
            };
            app.HelpOption("-?|-h|--help");

            AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) =>
            {
                var exception = (Exception) eventArgs.ExceptionObject;
                Console.Write(exception.Message);
                Environment.Exit(1);
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

            app.OnExecute(() => {

                
                if (app.Options.Any(o => !o.HasValue()))
                {
                    app.ShowHint();
                }
                else
                {
                    var directory = new DirectoryInfo(breachCompilationDataPath.Value());
                    foreach (var fileInfo in directory.GetFiles("*"))
                    {
                        var records = new List<KeyValuePair<string, string>>();
                        using (var file = new StreamReader(fileInfo.FullName))
                        {
                            string line;
                            while ((line = file.ReadLine()) != null)
                            {
                                var chunks = line.Split(new[] {":", ";"}, 2, StringSplitOptions.RemoveEmptyEntries);
                                if (chunks.Length == 2)
                                {
                                    var user = Escape(chunks[0]);
                                    var pass = Escape(chunks[1]);

                                    records.Add(new KeyValuePair<string, string>(user, pass));
                                }
                            }
                        }

                        var query = $"INSERT INTO {mysqlTable.Value()}(user, pass) VALUES ";
                        query += string.Join(",", records.Select(r => $"('{r.Key}','{r.Value}')"));

                        var passwordString = mysqlPassword.HasValue() ? $"-p {mysqlPassword.Value()}" : string.Empty;
                        var command =
                            $"{query} | mysql -u {mysqlUsername.Value()} {passwordString} {mysqlDatabase.Value()}";
                        ExecuteBashCommand(command);
                    }
                }

                return 0;
            });
        }

        private static string Escape(string data)
        {
            return data.Replace("'", "\'");
        }

        private static string ExecuteBashCommand(string command)
        {
            command = command.Replace("\"", "\"\"");

            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = "-c \"" + command + "\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            proc.Start();
            proc.WaitForExit();

            return proc.StandardOutput.ReadToEnd();
        }
    }
}
