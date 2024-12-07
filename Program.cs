using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using CommandLine;
using ConsoleMarkdownRenderer;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using Newtonsoft.Json;
using YamlDotNet.Serialization;

namespace SqlParseTree
{
    public enum Format
    {
        Json,
        Yaml,
        Html,
        Md,
    }

    public enum LogDestination
    {
        None,
        StdOut,
        StdError,
        Output,
    }

    public class Options 
    { 
        [Option('f', "format", Required = false, HelpText = "Output Format, defaults to Json")]
        public Format Format {get; set;}

        [Option('t', "to-file", Required = false, HelpText = "Write the output to file")]
        public bool ToFile {get; set;}

        [Option('o', "output-path", Required = false, HelpText = "The File path to use for output, defaults to console when to-file is false, and out.{format} when it is true")]
        public string? OutputPath {get; set;}

        [Option('l', "log-destination", Required = false, HelpText = "Where to send the log to")]
        public LogDestination LogDestination {get; set;}
    }

    class Program
    {
        static void Main(string[] args)
            => new Parser()
                .ParseArguments<Options>(args)
                .WithParsed(Run)
                .WithNotParsed(HandleParseError);

        static private void HandleParseError(IEnumerable<Error> errs)
        {
            Displayer.DisplayMarkdownAsync(new Uri(Path.Combine(AppContext.BaseDirectory, "README.md"))).GetAwaiter().GetResult();
            Console.WriteLine("Errors:");
            Console.WriteLine(errs.ToYaml());
        }

        private static void Run(Options options)
        {
            var watch = Stopwatch.StartNew();
            var log = new StringBuilder();

            if (!Console.IsInputRedirected)
            {
                LogError(log, "Input is not redirected", exitCode: 1);
                return;
            }

            // TODO: we should find some way to allow picking the version of the parser
            TSqlParser parser = new TSql150Parser(initialQuotedIdentifiers: true);

            TSqlFragment fragment;
            IList<ParseError> errors;

            using (var stream = Console.OpenStandardInput())
            using (var reader = new StreamReader( stream ))
            {
                fragment = parser.Parse(reader, out errors);
            }

            log.AppendLine($"Parse took: {watch.Elapsed}");

            if (errors.Any())
            {
                foreach (var error in errors)
                {
                    LogError(log, $"{error.Line},{error.Line}:{error.Offset} {error.Number} {error.Message}", exitCode: 2);
                }
                return;
            }

            var parseData = SqlParser.Parse(fragment, log);

            var output = options.Format switch
            {
                Format.Json => parseData.ToJson(),
                Format.Yaml => parseData.ToYaml(),
                Format.Html => HtmlFormatter.Format(parseData, log),
                Format.Md => MarkdownFormatter.Format(parseData, log),
                _ => throw new NotImplementedException($"Unknown format {options.Format}"),
            };

            if (options.ToFile || !string.IsNullOrEmpty(options.OutputPath))
            {
                var fileName = options.OutputPath ?? $"out.{options.Format.ToString().ToLower()}";
                File.WriteAllText(fileName, output);
                Console.WriteLine(Path.GetFullPath(fileName));

                if (options.LogDestination == LogDestination.Output)
                {
                    File.AppendAllText(fileName, log.ToString());
                }
            }
            else
            {
                if (options.Format == Format.Md)
                {
                    watch.Restart();
                    Displayer.DisplayMarkdownAsync(output, new Uri(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar)).GetAwaiter().GetResult();
                    log.AppendLine($"Display Markdown took: {watch.Elapsed}");
                }
                else
                {
                    Console.WriteLine(output);
                }

                if (options.LogDestination == LogDestination.Output)
                {
                    Console.WriteLine(log.ToString());
                }
            }

            switch (options.LogDestination)
            {
                case LogDestination.None:   // Nothing to do
                case LogDestination.Output: // Already Did it
                    break;

                case LogDestination.StdOut:
                    Console.WriteLine(log.ToString());
                    break;

                case LogDestination.StdError:
                    Console.Error.WriteLine(log.ToString());
                    break;

                default:
                    throw new NotImplementedException($"Unknown log destination {options.LogDestination}");
            }
        }

        private static void LogError(StringBuilder log, string message, int exitCode)
        {
            Console.Error.WriteLine(message);
            log.AppendLine($"ERROR: {message}");
            Environment.ExitCode = exitCode;
        }
    }

    public static class Extensions
    {
        static Extensions()
        {
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings{
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore,
            };
        }

        public static string AsText(this TSqlFragment fragment)
        {
            var builder = new StringBuilder();
            if (fragment.FirstTokenIndex >= 0)
            {
                for (int i = fragment.FirstTokenIndex; i <= fragment.LastTokenIndex; i++)
                {
                    builder.Append(fragment.ScriptTokenStream[i].Text);
                }
            }
            return builder.ToString();
        }

        public static string ToJson(this object obj) => JsonConvert.SerializeObject(obj);

        public static string ToYaml(this object obj)
        {
            ISerializer serializer = new SerializerBuilder()
                .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults)
                .WithMaximumRecursion(500) // prc_iSleepIfBusy got over 50
                .Build();
            return serializer.Serialize(obj);
        }
    }
}
