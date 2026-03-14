using System;
using System.IO;
using Xunit;
using DINBoard.Services;
using DINBoard.Models;

namespace Avalonia.Tests
{
    public class SymbolImportServiceTests : IDisposable
    {
        private readonly string _tempDir;

        public SymbolImportServiceTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "AvaloniaTests_" + Guid.NewGuid());
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                try { Directory.Delete(_tempDir, true); } catch { }
            }
        }

        [Fact]
        public void ImportFromFile_ShouldReturnNull_WhenFileDoesNotExist()
        {
            var service = new SymbolImportService();
            var result = service.ImportFromFile("non_existent_file.svg");
            Assert.Null(result);
        }

        [Fact]
        public void ImportFromFile_ShouldReturnSymbol_WhenSvgExists()
        {
            var service = new SymbolImportService();
            string filePath = Path.Combine(_tempDir, "test.svg");
            File.WriteAllText(filePath, "<svg width='100' height='100'></svg>");

            var result = service.ImportFromFile(filePath);

            Assert.NotNull(result);
            Assert.Equal("test", result.Label);
            Assert.Equal(filePath, result.VisualPath);
        }

        [Fact]
        public void ImportFromFile_ShouldExtractDefaultParameters()
        {
            var service = new SymbolImportService();
            string filePath = Path.Combine(_tempDir, "param_test.svg");
            File.WriteAllText(filePath, "<svg>{{CURRENT}}</svg>");

            var result = service.ImportFromFile(filePath);

            Assert.NotNull(result);
            Assert.True(result.Parameters.ContainsKey("CURRENT"));
            Assert.Equal("40A", result.Parameters["CURRENT"]);
        }
    }
}
