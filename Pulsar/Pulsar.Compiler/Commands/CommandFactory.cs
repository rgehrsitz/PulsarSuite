// File: Pulsar.Compiler/Commands/CommandFactory.cs

using Serilog;
using System;
using System.Collections.Generic;

namespace Pulsar.Compiler.Commands
{
    public class CommandFactory
    {
        private readonly ILogger _logger;

        public CommandFactory(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public ICommand CreateCommand(string commandName)
        {
            return commandName.ToLowerInvariant() switch
            {
                "compile" => new CompileCommand(_logger),
                "validate" => new ValidateCommand(_logger),
                "init" => new InitCommand(_logger),
                "generate" => new GenerateCommand(_logger),
                "beacon" => new BeaconCommand(_logger),
                "test" => new TestCommand(_logger),
                _ => throw new ArgumentException($"Unknown command: {commandName}", nameof(commandName))
            };
        }
    }

    public interface ICommand
    {
        Task<int> RunAsync(Dictionary<string, string> options);
    }
}