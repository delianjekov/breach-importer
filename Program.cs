using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace BreachImporter
{
    class Program
    {
        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) =>
            {
                var exception = (Exception) eventArgs.ExceptionObject;
                Console.Write(exception.Message);
                Environment.Exit(1);
            };

            var breachCompilationDataPath = GetArgument("path", args);
            var mysqlDatabase = GetArgument("database", args);
            var mysqlTable = GetArgument("table", args);
            var mysqlUsername = GetArgument("user", args);
            var mysqlPassword = GetArgument("password", args, true);

            var directory = new DirectoryInfo(breachCompilationDataPath);
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

                var query = $"INSERT INTO {mysqlTable}(user, pass) VALUES ";
                query += string.Join(",", records.Select(r => $"('{r.Key}','{r.Value}')"));

                var passwordString = string.IsNullOrWhiteSpace(mysqlPassword) ? string.Empty : $"-p {mysqlPassword}";
                var command = $"{query} | mysql -u {mysqlUsername} {passwordString} {mysqlDatabase}";
                ExecuteBashCommand(command);
            }
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

        private static string GetArgument(string arg, string[] args, bool optional = false)
        {
            var item = args.FirstOrDefault(a => a.StartsWith($"--{arg}"));
            if (item == null && !optional)
                throw new ArgumentException($"Argument {arg} is obligatory.{Environment.NewLine}{Environment.NewLine}{GetSampleUsage()}");

            return item?.Split("=", StringSplitOptions.RemoveEmptyEntries).Last();
        }

        private static string GetSampleUsage()
        {
            return $"Usage:{Environment.NewLine}     BreachImporter [OPTIONS]{Environment.NewLine}{Environment.NewLine}Options:" +
                   $"{Environment.NewLine}     --path {Environment.NewLine}          The path to the data folder of the breach compilation, e.g. --path=/home/root/breachcompilation/data" +
                   $"{Environment.NewLine}     --database {Environment.NewLine}          The name of the MySql database to import to, e.g. --database=breach" +
                   $"{Environment.NewLine}     --table {Environment.NewLine}          The name of the MySql table to import data to (the table has to have two string columns named user and pass), e.g. --table=user" +
                   $"{Environment.NewLine}     --username {Environment.NewLine}          The MySql username to use for the import process, e.g. --username=breach" +
                   $"{Environment.NewLine}     --password {Environment.NewLine}          The MySql Password to use for the import process, e.g. --password=secret{Environment.NewLine}";
        }
    }
}
