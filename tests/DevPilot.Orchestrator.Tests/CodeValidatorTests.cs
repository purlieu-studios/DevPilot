using DevPilot.Orchestrator.Validation;
using FluentAssertions;

namespace DevPilot.Orchestrator.Tests;

/// <summary>
/// Tests for CodeValidator - validates code in workspace before building.
/// </summary>
public sealed class CodeValidatorTests : IDisposable
{
    private readonly string _testWorkspacePath;

    public CodeValidatorTests()
    {
        _testWorkspacePath = Path.Combine(Path.GetTempPath(), $"validator-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testWorkspacePath);
    }

    #region ValidateWorkspace Tests

    [Fact]
    public void ValidateWorkspace_ValidTestProject_ReturnsSuccess()
    {
        // Arrange
        CreateTestProject("MyApp.Tests", withXunitDependencies: true);
        CreateTestFile("MyApp.Tests/CalculatorTests.cs", @"
using Xunit;

public class CalculatorTests
{
    [Fact]
    public void Add_ReturnsSum()
    {
        Assert.Equal(5, 2 + 3);
    }
}");

        var validator = new CodeValidator();

        // Act
        var result = validator.ValidateWorkspace(_testWorkspacePath);

        // Assert
        result.Success.Should().BeTrue();
        result.Summary.Should().Contain("passed");
    }

    [Fact]
    public void ValidateWorkspace_TestFileWithoutProjectFile_ReturnsFailure()
    {
        // Arrange
        CreateTestFile("CalculatorTests.cs", @"
using Xunit;

public class CalculatorTests
{
    [Fact]
    public void Test() { }
}");

        var validator = new CodeValidator();

        // Act
        var result = validator.ValidateWorkspace(_testWorkspacePath);

        // Assert
        result.Success.Should().BeFalse();
        result.Details.Should().Contain("not in a directory with a .csproj file");
        result.Details.Should().Contain("CalculatorTests.cs");
    }

    [Fact]
    public void ValidateWorkspace_TestProjectMissingXunit_ReturnsFailure()
    {
        // Arrange
        CreateTestProject("MyApp.Tests", withXunitDependencies: false);
        CreateTestFile("MyApp.Tests/CalculatorTests.cs", @"
using Xunit;

public class CalculatorTests
{
    [Fact]
    public void Test() { }
}");

        var validator = new CodeValidator();

        // Act
        var result = validator.ValidateWorkspace(_testWorkspacePath);

        // Assert
        result.Success.Should().BeFalse();
        result.Details.Should().Contain("Missing xUnit package reference");
    }

    [Fact]
    public void ValidateWorkspace_TestProjectMissingTestSdk_ReturnsFailure()
    {
        // Arrange
        var projectContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""xunit"" Version=""2.9.2"" />
  </ItemGroup>
</Project>";

        CreateDirectory("MyApp.Tests");
        File.WriteAllText(Path.Combine(_testWorkspacePath, "MyApp.Tests", "MyApp.Tests.csproj"), projectContent);
        CreateTestFile("MyApp.Tests/CalculatorTests.cs", @"
using Xunit;

public class CalculatorTests
{
    [Fact]
    public void Test() { }
}");

        var validator = new CodeValidator();

        // Act
        var result = validator.ValidateWorkspace(_testWorkspacePath);

        // Assert
        result.Success.Should().BeFalse();
        result.Details.Should().Contain("Missing Microsoft.NET.Test.Sdk package reference");
    }

    [Fact]
    public void ValidateWorkspace_EmptyWorkspace_ReturnsSuccess()
    {
        // Arrange
        var validator = new CodeValidator();

        // Act
        var result = validator.ValidateWorkspace(_testWorkspacePath);

        // Assert
        result.Success.Should().BeTrue("no files to validate");
    }

    [Fact]
    public void ValidateWorkspace_NonExistentPath_ReturnsFailure()
    {
        // Arrange
        var validator = new CodeValidator();

        // Act
        var result = validator.ValidateWorkspace(@"C:\NonExistent\Path");

        // Assert
        result.Success.Should().BeFalse();
        result.Summary.Should().Contain("does not exist");
    }

    [Fact]
    public void ValidateWorkspace_OnlySourceFiles_ReturnsSuccess()
    {
        // Arrange
        CreateTestFile("Calculator.cs", @"
public class Calculator
{
    public int Add(int a, int b) => a + b;
}");

        var validator = new CodeValidator();

        // Act
        var result = validator.ValidateWorkspace(_testWorkspacePath);

        // Assert
        result.Success.Should().BeTrue("source files without test attributes are ignored");
    }

    #endregion

    #region ValidateModifiedFiles Tests

    [Fact]
    public void ValidateModifiedFiles_ValidTestFile_ReturnsSuccess()
    {
        // Arrange
        CreateTestProject("MyApp.Tests", withXunitDependencies: true);
        CreateTestFile("MyApp.Tests/CalculatorTests.cs", @"
using Xunit;

public class CalculatorTests
{
    [Fact]
    public void Test() { }
}");

        var validator = new CodeValidator();
        var modifiedFiles = new List<string> { @"MyApp.Tests\CalculatorTests.cs" };

        // Act
        var result = validator.ValidateModifiedFiles(_testWorkspacePath, modifiedFiles);

        // Assert
        result.Success.Should().BeTrue();
    }

    [Fact]
    public void ValidateModifiedFiles_TestFileInWrongLocation_ReturnsFailure()
    {
        // Arrange
        CreateTestFile("CalculatorTests.cs", @"
using Xunit;

public class CalculatorTests
{
    [Fact]
    public void Test() { }
}");

        var validator = new CodeValidator();
        var modifiedFiles = new List<string> { "CalculatorTests.cs" };

        // Act
        var result = validator.ValidateModifiedFiles(_testWorkspacePath, modifiedFiles);

        // Assert
        result.Success.Should().BeFalse();
        result.Details.Should().Contain("not in a directory with a .csproj file");
    }

    [Fact]
    public void ValidateModifiedFiles_NonCSharpFiles_Skipped()
    {
        // Arrange
        CreateTestFile("README.md", "# Documentation");
        CreateTestFile("config.json", "{}");

        var validator = new CodeValidator();
        var modifiedFiles = new List<string> { "README.md", "config.json" };

        // Act
        var result = validator.ValidateModifiedFiles(_testWorkspacePath, modifiedFiles);

        // Assert
        result.Success.Should().BeTrue("non-C# files should be skipped");
    }

    [Fact]
    public void ValidateModifiedFiles_EmptyList_ReturnsSuccess()
    {
        // Arrange
        var validator = new CodeValidator();

        // Act
        var result = validator.ValidateModifiedFiles(_testWorkspacePath, new List<string>());

        // Assert
        result.Success.Should().BeTrue("no files to validate");
    }

    [Fact]
    public void ValidateModifiedFiles_MixedValidInvalid_ReturnsFailure()
    {
        // Arrange
        CreateTestProject("MyApp.Tests", withXunitDependencies: true);
        CreateTestFile("MyApp.Tests/ValidTests.cs", @"
using Xunit;

public class ValidTests
{
    [Fact]
    public void Test() { }
}");
        CreateTestFile("InvalidTests.cs", @"
using Xunit;

public class InvalidTests
{
    [Fact]
    public void Test() { }
}");

        var validator = new CodeValidator();
        var modifiedFiles = new List<string>
        {
            @"MyApp.Tests\ValidTests.cs",
            "InvalidTests.cs"
        };

        // Act
        var result = validator.ValidateModifiedFiles(_testWorkspacePath, modifiedFiles);

        // Assert
        result.Success.Should().BeFalse("one invalid file should fail validation");
        result.Details.Should().Contain("InvalidTests.cs");
    }

    [Fact]
    public void ValidateModifiedFiles_NullModifiedFiles_ThrowsArgumentNullException()
    {
        // Arrange
        var validator = new CodeValidator();

        // Act
        var act = () => validator.ValidateModifiedFiles(_testWorkspacePath, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Test Attribute Detection

    [Fact]
    public void ValidateWorkspace_DetectsFactAttribute()
    {
        // Arrange
        CreateTestFile("Tests.cs", @"
public class Tests
{
    [Fact]
    public void Test() { }
}");

        var validator = new CodeValidator();

        // Act
        var result = validator.ValidateWorkspace(_testWorkspacePath);

        // Assert
        result.Success.Should().BeFalse("test file without project should fail");
        result.Details.Should().Contain("Tests.cs");
    }

    [Fact]
    public void ValidateWorkspace_DetectsTheoryAttribute()
    {
        // Arrange
        CreateTestFile("Tests.cs", @"
public class Tests
{
    [Theory]
    [InlineData(1, 2)]
    public void Test(int a, int b) { }
}");

        var validator = new CodeValidator();

        // Act
        var result = validator.ValidateWorkspace(_testWorkspacePath);

        // Assert
        result.Success.Should().BeFalse("test file without project should fail");
    }

    [Fact]
    public void ValidateWorkspace_DetectsTestAttribute()
    {
        // Arrange
        CreateTestFile("Tests.cs", @"
public class Tests
{
    [Test]
    public void Test() { }
}");

        var validator = new CodeValidator();

        // Act
        var result = validator.ValidateWorkspace(_testWorkspacePath);

        // Assert
        result.Success.Should().BeFalse("test file without project should fail");
    }

    [Fact]
    public void ValidateWorkspace_IgnoresCaseInAttributes()
    {
        // Arrange
        CreateTestFile("Tests.cs", @"
public class Tests
{
    [fact]
    public void Test() { }
}");

        var validator = new CodeValidator();

        // Act
        var result = validator.ValidateWorkspace(_testWorkspacePath);

        // Assert
        result.Success.Should().BeFalse("lowercase [fact] should be detected");
    }

    [Fact]
    public void ValidateWorkspace_TestInNestedSubdirectory_ValidatesCorrectly()
    {
        // Arrange
        CreateTestProject("MyApp.Tests", withXunitDependencies: true);
        CreateTestFile("MyApp.Tests/Unit/CalculatorTests.cs", @"
using Xunit;

public class CalculatorTests
{
    [Fact]
    public void Test() { }
}");

        var validator = new CodeValidator();

        // Act
        var result = validator.ValidateWorkspace(_testWorkspacePath);

        // Assert
        result.Success.Should().BeTrue("nested test files should find parent .csproj");
    }

    [Fact]
    public void ValidateWorkspace_NullWorkspacePath_ThrowsArgumentException()
    {
        // Arrange
        var validator = new CodeValidator();

        // Act
        var act = () => validator.ValidateWorkspace(null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    // Helper methods

    private void CreateTestProject(string projectName, bool withXunitDependencies)
    {
        CreateDirectory(projectName);

        var xunitPackage = withXunitDependencies
            ? @"<PackageReference Include=""xunit"" Version=""2.9.2"" />
    <PackageReference Include=""Microsoft.NET.Test.Sdk"" Version=""17.14.1"" />"
            : string.Empty;

        var projectContent = $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    {xunitPackage}
  </ItemGroup>
</Project>";

        File.WriteAllText(Path.Combine(_testWorkspacePath, projectName, $"{projectName}.csproj"), projectContent);
    }

    private void CreateTestFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(_testWorkspacePath, relativePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (directory != null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(fullPath, content);
    }

    private void CreateDirectory(string relativePath)
    {
        var fullPath = Path.Combine(_testWorkspacePath, relativePath);
        Directory.CreateDirectory(fullPath);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testWorkspacePath))
            {
                Directory.Delete(_testWorkspacePath, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup failures
        }
    }
}
