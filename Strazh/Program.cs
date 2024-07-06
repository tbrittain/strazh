﻿using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;
using Strazh.Analysis;

namespace Strazh
{
    public class Program
    {

        public static async Task Main(params string[] args)
        {
            var rootCommand = new RootCommand();

            var optionCredentials = new Option<string>("--credentials", "required information in format `dbname:user:password` to connect to Neo4j Database");
            optionCredentials.AddAlias("-c");
            optionCredentials.IsRequired = true;
            rootCommand.Add(optionCredentials);

            var optionMode = new Option<string>("--tier", "optional flag as `project` or `code` or 'all' (default `all`) selected tier to scan in a codebase");
            optionMode.AddAlias("-t");
            optionMode.IsRequired = false;
            rootCommand.Add(optionMode);

            var optionDelete = new Option<string>("--delete", "optional flag as `true` or `false` or no flag (default `true`) to delete data in graph before execution");
            optionDelete.AddAlias("-d");
            optionDelete.IsRequired = false;
            rootCommand.Add(optionDelete);

            var optionSolution = new Option<string>("--solution", "optional absolute path to only one `.sln` file (can't be used together with -p / --projects)");
            optionSolution.AddAlias("-s");
            optionSolution.IsRequired = false;
            rootCommand.Add(optionSolution);

            var optionProjects = new Option<string[]>("--projects", "optional list of absolute path to one or many `.csproj` files (can't be used together with -s / --solution)");
            optionProjects.AddAlias("-p");
            optionProjects.IsRequired = false;
            rootCommand.Add(optionProjects);

            rootCommand.Handler = CommandHandler.Create<string, string, string, string, string[]>(BuildKnowledgeGraph);
            await rootCommand.InvokeAsync(args);
        }

        private static async Task BuildKnowledgeGraph(string credentials, string tier, string delete, string solution, string[] projects)
        {
            try
            {
                var config = new AnalyzerConfig(
                       credentials,
                       tier,
                       delete,
                       solution,
                       projects
                   );
                if (!config.IsValid)
                {
                    Console.WriteLine("Please submit only one thing: `--solution` (-s) or `--projects` (-p)");
                    return;
                }
                var isNeo4jReady = await Healthcheck.IsNeo4jReady();
                if (!isNeo4jReady)
                {
                    Console.WriteLine("Strazh failed to start. There is no Neo4j instance ready to use.");
                    return;
                }

                Console.WriteLine($"Brewing a Code Knowledge Graph of tier \"{config.Tier}\".");                
                await Analyzer.Analyze(config);
                Console.WriteLine("Code Knowledge Graph created.");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
}
