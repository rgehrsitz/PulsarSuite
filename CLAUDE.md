# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Test Commands
- Build: `dotnet build`
- Run compiler: `dotnet run --project Pulsar.Compiler`
- Run all tests: `dotnet test`
- Run specific test: `dotnet test --filter "FullyQualifiedName=Namespace.Class.Method"`
- Run tests by category: `dotnet test --filter "Category=Integration"`
- AOT build: `dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishAot=true`

## Code Style Guidelines
- **Naming**: PascalCase for types/methods/properties, IPrefix for interfaces, _camelCase for private fields
- **Redis Keys**: Use prefixes: `input:`, `output:`, `state:`, `buffer:`
- **Organization**: Namespace matches folder structure, group related functionality in subdirectories
- **Error Handling**: Custom exceptions, proper logging before throws
- **Dependencies**: Constructor injection, ILogger for logging, dispose resources properly
- **Documentation**: XML comments on public APIs
- **Testing**: Use xUnit with descriptive names following `ClassName_Scenario_ExpectedResult` pattern
- **Types**: Prefer nullable reference types and immutable collections where appropriate
- **Async**: Follow standard async/await patterns with proper cancellation support

*Note: Redis integration tests require Docker with Redis container*