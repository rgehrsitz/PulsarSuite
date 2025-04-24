// File: Pulsar.Tests/Core/ErrorHandlingTests.cs

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Pulsar.Compiler.Exceptions;
using Xunit;

namespace Pulsar.Tests.Core
{
    public class ErrorHandlingTests
    {
        [Fact]
        public void ValidationException_StoresContext()
        {
            // Arrange
            var context = new Dictionary<string, object>
            {
                { "key1", "value1" },
                { "key2", 42 }
            };
            
            // Act
            var exception = new ValidationException("Test error", "ValidationError", context);
            
            // Assert
            Assert.Equal("Test error", exception.Message);
            Assert.Equal("ValidationError", exception.ErrorType);
            Assert.Equal("value1", exception.Context["key1"]);
            Assert.Equal(42, exception.Context["key2"]);
        }
        
        [Fact]
        public void ValidationException_HasDefaultErrorType()
        {
            // Act
            var exception = new ValidationException("Test error");
            
            // Assert
            Assert.Equal("ValidationError", exception.ErrorType);
        }
        
        [Fact]
        public void RuleCompilationException_HasCorrectErrorType()
        {
            // Act
            var exception = new RuleCompilationException("Compilation error");
            
            // Assert
            Assert.Equal("CompilationError", exception.ErrorType);
        }

        [Fact]
        public void SafeExecute_ReturnsResult_WhenNoException()
        {
            // Arrange & Act
            var result = ErrorHandling.SafeExecute(
                () => 42,
                -1, // Error result
                new Dictionary<string, object> { { "test", "context" } }
            );
            
            // Assert
            Assert.Equal(42, result);
        }
        
        [Fact]
        public void SafeExecute_ReturnsErrorResult_WhenExceptionThrown()
        {
            // Arrange & Act
            var result = ErrorHandling.SafeExecute(
                () => throw new InvalidOperationException("Test exception"),
                -1, // Error result
                new Dictionary<string, object> { { "test", "context" } }
            );
            
            // Assert
            Assert.Equal(-1, result);
        }

        [Fact]
        public void SafeExecute_AllowsSpecificExceptions()
        {
            // Should not capture ArgumentException because it's in allowed exceptions
            Assert.Throws<ArgumentException>(() => ErrorHandling.SafeExecute(
                () => throw new ArgumentException("Expected exception"),
                -1,
                new Dictionary<string, object>(),
                "TestOperation",
                typeof(ArgumentException)
            ));
            
            // Should capture and return error result for other exceptions
            var result = ErrorHandling.SafeExecute(
                () => throw new InvalidOperationException("Unexpected exception"),
                -1,
                new Dictionary<string, object>(),
                "TestOperation",
                typeof(ArgumentException)
            );
            
            Assert.Equal(-1, result);
        }
        
        [Fact]
        public async Task SafeExecuteAsync_ReturnsResult_WhenNoException()
        {
            // Arrange & Act
            var result = await ErrorHandling.SafeExecuteAsync(
                async () => {
                    await Task.Delay(1);
                    return 42;
                },
                -1, // Error result
                new Dictionary<string, object> { { "test", "context" } }
            );
            
            // Assert
            Assert.Equal(42, result);
        }
        
        [Fact]
        public async Task SafeExecuteAsync_ReturnsErrorResult_WhenExceptionThrown()
        {
            // Arrange & Act
            var result = await ErrorHandling.SafeExecuteAsync(
                async () => {
                    await Task.Delay(1);
                    throw new InvalidOperationException("Test exception");
                },
                -1, // Error result
                new Dictionary<string, object> { { "test", "context" } }
            );
            
            // Assert
            Assert.Equal(-1, result);
        }
        
        [Fact]
        public void Validate_ThrowsValidationException_WhenConditionIsFalse()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ValidationException>(() => 
                ErrorHandling.Validate(false, "Test validation failed")
            );
            
            Assert.Equal("ValidationError", ex.ErrorType);
            Assert.Contains("Test validation failed", ex.Message);
        }
        
        [Fact]
        public void ValidateNotNull_ThrowsArgumentNullException_WhenNull()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => 
                ErrorHandling.ValidateNotNull(null, "testParam")
            );
            
            Assert.Contains("testParam", ex.ParamName);
        }
        
        [Fact]
        public void ValidateNotNullOrEmpty_ThrowsArgumentException_WhenEmpty()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => 
                ErrorHandling.ValidateNotNullOrEmpty("", "testParam")
            );
            
            Assert.Contains("testParam", ex.Message);
            Assert.Equal("testParam", ex.ParamName);
        }
        
        [Fact]
        public void SafeExecute_Action_ReturnsTrueWhenSuccessful()
        {
            // Act
            var result = ErrorHandling.SafeExecute(
                () => { /* Do nothing */ },
                operationName: "TestOperation"
            );
            
            // Assert
            Assert.True(result);
        }
        
        [Fact]
        public void SafeExecute_Action_ReturnsFalseWhenExceptionThrown()
        {
            // Act
            var result = ErrorHandling.SafeExecute(
                () => { throw new InvalidOperationException("Test exception"); },
                operationName: "TestOperation"
            );
            
            // Assert
            Assert.False(result);
        }
        
        [Fact]
        public void SafeExecute_Action_WithContext_ReturnsTrueWhenSuccessful()
        {
            // Arrange
            var context = new Dictionary<string, object> { { "operation", "test" } };
            
            // Act
            var result = ErrorHandling.SafeExecute(
                () => { /* Do nothing */ },
                context,
                "TestOperation"
            );
            
            // Assert
            Assert.True(result);
        }
        
        [Fact]
        public void SafeExecute_Action_RethrowsWhenConfigured()
        {
            // Arrange
            var context = new Dictionary<string, object> { { "operation", "test" } };
            
            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => ErrorHandling.SafeExecute(
                () => { throw new InvalidOperationException("Test exception"); },
                context,
                "TestOperation",
                true // rethrow=true
            ));
        }
        
        [Fact]
        public async Task SafeExecuteAsync_Action_ReturnsTrueWhenSuccessful()
        {
            // Arrange
            var context = new Dictionary<string, object> { { "operation", "test" } };
            
            // Act
            var result = await ErrorHandling.SafeExecuteAsync(
                async () => { await Task.Delay(1); },
                context
            );
            
            // Assert
            Assert.True(result);
        }
        
        [Fact]
        public async Task SafeExecuteAsync_Action_ReturnsFalseWhenExceptionThrown()
        {
            // Arrange
            var context = new Dictionary<string, object> { { "operation", "test" } };
            
            // Act
            var result = await ErrorHandling.SafeExecuteAsync(
                async () => { 
                    await Task.Delay(1);
                    throw new InvalidOperationException("Test exception"); 
                },
                context
            );
            
            // Assert
            Assert.False(result);
        }
        
        [Fact]
        public async Task SafeExecuteAsync_Action_RethrowsWhenConfigured()
        {
            // Arrange
            var context = new Dictionary<string, object> { { "operation", "test" } };
            
            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => ErrorHandling.SafeExecuteAsync(
                async () => { 
                    await Task.Delay(1);
                    throw new InvalidOperationException("Test exception"); 
                },
                context,
                "TestOperation",
                true // rethrow=true
            ));
        }
        
        [Fact]
        public void ValidationExceptionToString_IncludesContextData()
        {
            // Arrange
            var context = new Dictionary<string, object>
            {
                { "key1", "value1" },
                { "key2", 42 }
            };
            var exception = new ValidationException("Test error", "CustomError", context);
            
            // Act
            string result = exception.ToString();
            
            // Assert
            Assert.Contains("CustomError", result);
            Assert.Contains("Test error", result);
            Assert.Contains("key1", result);
            Assert.Contains("key2", result);
        }
        
        [Fact]
        public void ConfigurationException_HasCorrectErrorType()
        {
            // Act
            var exception = new ConfigurationException("Config error");
            
            // Assert
            Assert.Equal("ConfigurationError", exception.ErrorType);
            Assert.Equal("Config error", exception.Message);
        }
        
        [Fact]
        public void ConfigurationException_WithContext_PreservesContext()
        {
            // Arrange
            var context = new Dictionary<string, object> { { "config", "section" } };
            
            // Act
            var exception = new ConfigurationException("Config error", context);
            
            // Assert
            Assert.Equal("ConfigurationError", exception.ErrorType);
            Assert.Equal("Config error", exception.Message);
            Assert.Equal("section", exception.Context["config"]);
        }
        
        [Fact]
        public void ValidationException_WithInnerException_PreservesInnerException()
        {
            // Arrange
            var inner = new ArgumentException("Inner error");
            
            // Act
            var exception = new ValidationException("Outer error", inner);
            
            // Assert
            Assert.Equal("ValidationError", exception.ErrorType);
            Assert.Equal("Outer error", exception.Message);
            Assert.Same(inner, exception.InnerException);
        }
        
        [Fact]
        public void ValidationException_WithInnerExceptionAndContext_PreservesBoth()
        {
            // Arrange
            var inner = new ArgumentException("Inner error");
            var context = new Dictionary<string, object> { { "test", "value" } };
            
            // Act
            var exception = new ValidationException("Outer error", inner, "CustomError", context);
            
            // Assert
            Assert.Equal("CustomError", exception.ErrorType);
            Assert.Equal("Outer error", exception.Message);
            Assert.Same(inner, exception.InnerException);
            Assert.Equal("value", exception.Context["test"]);
        }
    }
}