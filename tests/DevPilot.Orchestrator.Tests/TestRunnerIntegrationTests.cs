using DevPilot.Core;
using DevPilot.Orchestrator;
using FluentAssertions;
using Xunit;

namespace DevPilot.Orchestrator.Tests;

/// <summary>
/// Integration tests for TestRunner that execute real dotnet build/test commands.
/// These tests validate critical functionality that was broken in PR #61 (coverage collection).
///
/// IMPORTANT: These tests create temporary workspaces and run actual dotnet commands.
/// They are slower than unit tests but catch real integration issues.
/// </summary>
[Trait("Category", "Integration")]
public sealed class TestRunnerIntegrationTests : IDisposable
{
    private readonly string _testBaseDirectory;
    private readonly List<string> _workspacesToCleanup;

    public TestRunnerIntegrationTests()
    {
        _testBaseDirectory = Path.Combine(Path.GetTempPath(), "testrunner-tests", Guid.NewGuid().ToString());
        _workspacesToCleanup = new List<string>();
        Directory.CreateDirectory(_testBaseDirectory);
    }

    #region Coverage Collection Tests (PR #61 Regression Prevention)

    /// <summary>
    /// REGRESSION TEST: Validates that TestRunner correctly collects coverage data.
    /// This test would have caught the PR #61 bug where Coverage was always null.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task TestRunner_SingleSolutionFile_CollectsCoverage()
    {
        // Arrange
        var workspaceRoot = CreateSimpleCalculatorWorkspace();

        // Act
        var result = await TestRunner.ExecuteTestsAsync(workspaceRoot);

        // Assert
        result.Pass.Should().BeTrue("all tests should pass");
        result.Coverage.Should().NotBeNull("coverage data must be collected");
        result.Coverage!.LineCoveragePercent.Should().BeGreaterThan(0, "some code should be covered");
        result.Coverage.BranchCoveragePercent.Should().BeGreaterThanOrEqualTo(0, "branch coverage is optional but should not be negative");
    }

    /// <summary>
    /// REGRESSION TEST: Validates solution file discovery when multiple .sln files exist.
    /// This is the EXACT bug that broke PR #61 - DevPilot.sln + original.sln caused ambiguity.
    /// NOTE: This test hangs in CI environment (Windows Server 2025) but passes locally in 3s.
    /// Marked LocalOnly until CI environment issue can be diagnosed.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Regression")]
    [Trait("Category", "LocalOnly")]
    public async Task TestRunner_MultipleSolutionFiles_ChoosesCorrectOne()
    {
        // Arrange
        var workspaceRoot = CreateWorkspaceWithMultipleSolutions();

        // Act
        var result = await TestRunner.ExecuteTestsAsync(workspaceRoot);

        // Assert - Should NOT fail with "multiple solution files" error
        result.Pass.Should().BeTrue("should handle multiple .sln files correctly");
        result.ErrorMessage.Should().BeNullOrWhiteSpace("should not have errors");
        result.Coverage.Should().NotBeNull("coverage should still be collected with multiple .sln files");
    }

    /// <summary>
    /// Validates that Directory.Build.props correctly applies coverlet.collector package.
    /// Tests the fix implemented in PR #61.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task TestRunner_WithDirectoryBuildProps_AppliesCoverletCollector()
    {
        // Arrange
        var workspaceRoot = CreateWorkspaceWithDirectoryBuildProps();

        // Act
        var result = await TestRunner.ExecuteTestsAsync(workspaceRoot);

        // Assert
        result.Pass.Should().BeTrue();
        result.Coverage.Should().NotBeNull("Directory.Build.props should enable coverage collection");

        // Verify coverage file was actually created
        var coverageFiles = Directory.GetFiles(Path.Combine(workspaceRoot, "TestResults"), "coverage.cobertura.xml", SearchOption.AllDirectories);
        coverageFiles.Should().NotBeEmpty("coverage.cobertura.xml should be generated");
    }

    #endregion

    #region Coverage Parsing Tests

    /// <summary>
    /// Tests that Cobertura XML parsing correctly converts line-rate/branch-rate to percentages.
    /// </summary>
    [Fact]
    public void TestRunner_ParsesCoberturaXml_CorrectlyCalculatesPercentages()
    {
        // Arrange
        var coverageXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <coverage line-rate="0.85" branch-rate="0.72" version="1.9" timestamp="1234567890">
              <packages>
                <package name="TestPackage" line-rate="0.85" branch-rate="0.72">
                </package>
              </packages>
            </coverage>
            """;

        var tempFile = Path.Combine(_testBaseDirectory, "test-coverage.xml");
        File.WriteAllText(tempFile, coverageXml);
        _workspacesToCleanup.Add(tempFile);

        // Act
        var coverageInfo = ParseCoverageFileViaReflection(tempFile);

        // Assert
        coverageInfo.Should().NotBeNull();
        coverageInfo!.LineCoveragePercent.Should().BeApproximately(85.0, 0.01, "0.85 line-rate should convert to 85%");
        coverageInfo.BranchCoveragePercent.Should().BeApproximately(72.0, 0.01, "0.72 branch-rate should convert to 72%");
    }

    #endregion

    #region Error Handling Tests

    /// <summary>
    /// Validates that build failures return detailed error messages for debugging.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task TestRunner_BuildFails_ReturnsDetailedErrorMessage()
    {
        // Arrange
        var workspaceRoot = CreateWorkspaceWithCompilationError();

        // Act
        var result = await TestRunner.ExecuteTestsAsync(workspaceRoot);

        // Assert
        result.Pass.Should().BeFalse("build should fail");
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace("should provide error details");
        result.ErrorMessage.Should().Contain("Build failed", "should indicate build failure");
        result.Coverage.Should().BeNull("coverage should not be collected when build fails");
    }

    /// <summary>
    /// Validates that workspaces with no test projects are handled gracefully.
    /// Note: TestRunner treats "no tests found" as a failure (no TRX file generated),
    /// which is reasonable behavior to alert users that something might be misconfigured.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task TestRunner_NoTests_ReturnsFailureWithDiagnostics()
    {
        // Arrange
        var workspaceRoot = CreateWorkspaceWithNoTests();

        // Act
        var result = await TestRunner.ExecuteTestsAsync(workspaceRoot);

        // Assert - No TRX file means failure (but not a crash)
        result.Pass.Should().BeFalse("no TRX file generated when no tests exist");
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace("should explain why no tests were found");
        result.ErrorMessage.Should().Contain("No TRX file found", "should indicate missing TRX file");
        result.TestResults.Should().BeEmpty("should have no test results when no tests run");
    }

    #endregion

    #region Helper Methods - Workspace Creation

    /// <summary>
    /// Creates a simple Calculator workspace with a test project that has 100% coverage.
    /// </summary>
    private string CreateSimpleCalculatorWorkspace()
    {
        var workspaceRoot = Path.Combine(_testBaseDirectory, "simple-calculator-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(workspaceRoot);
        _workspacesToCleanup.Add(workspaceRoot);

        // Create solution file
        var solutionFile = Path.Combine(workspaceRoot, "Calculator.sln");
        File.WriteAllText(solutionFile, @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
VisualStudioVersion = 17.0.31903.59
MinimumVisualStudioVersion = 10.0.40219.1
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""Calculator"", ""Calculator\Calculator.csproj"", ""{11111111-1111-1111-1111-111111111111}""
EndProject
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""Calculator.Tests"", ""Calculator.Tests\Calculator.Tests.csproj"", ""{22222222-2222-2222-2222-222222222222}""
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Release|Any CPU = Release|Any CPU
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{11111111-1111-1111-1111-111111111111}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{11111111-1111-1111-1111-111111111111}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{11111111-1111-1111-1111-111111111111}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{11111111-1111-1111-1111-111111111111}.Release|Any CPU.Build.0 = Release|Any CPU
		{22222222-2222-2222-2222-222222222222}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{22222222-2222-2222-2222-222222222222}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{22222222-2222-2222-2222-222222222222}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{22222222-2222-2222-2222-222222222222}.Release|Any CPU.Build.0 = Release|Any CPU
	EndGlobalSection
EndGlobal
");

        // Create main project
        var mainDir = Path.Combine(workspaceRoot, "Calculator");
        Directory.CreateDirectory(mainDir);
        File.WriteAllText(Path.Combine(mainDir, "Calculator.csproj"), @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
");
        File.WriteAllText(Path.Combine(mainDir, "Calculator.cs"), @"
namespace Calculator;

public class Calculator
{
    public int Add(int a, int b) => a + b;
    public int Subtract(int a, int b) => a - b;
}
");

        // Create test project with coverlet.collector
        var testDir = Path.Combine(workspaceRoot, "Calculator.Tests");
        Directory.CreateDirectory(testDir);
        File.WriteAllText(Path.Combine(testDir, "Calculator.Tests.csproj"), @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""Microsoft.NET.Test.Sdk"" Version=""17.11.0"" />
    <PackageReference Include=""xunit"" Version=""2.9.0"" />
    <PackageReference Include=""xunit.runner.visualstudio"" Version=""2.8.2"" />
    <PackageReference Include=""coverlet.collector"" Version=""6.0.2"">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include=""..\Calculator\Calculator.csproj"" />
  </ItemGroup>
</Project>
");
        File.WriteAllText(Path.Combine(testDir, "CalculatorTests.cs"), @"
using Xunit;

namespace Calculator.Tests;

public class CalculatorTests
{
    [Fact]
    public void Add_TwoNumbers_ReturnsSum()
    {
        var calc = new Calculator();
        var result = calc.Add(2, 3);
        Assert.Equal(5, result);
    }

    [Fact]
    public void Subtract_TwoNumbers_ReturnsDifference()
    {
        var calc = new Calculator();
        var result = calc.Subtract(5, 3);
        Assert.Equal(2, result);
    }
}
");

        return workspaceRoot;
    }

    /// <summary>
    /// Creates workspace with Calculator.sln + DevPilot.sln to test solution file discovery.
    /// This recreates the PR #61 bug scenario.
    /// </summary>
    private string CreateWorkspaceWithMultipleSolutions()
    {
        var workspaceRoot = CreateSimpleCalculatorWorkspace();

        // Add a second solution file (DevPilot.sln) that should be IGNORED
        var devPilotSln = Path.Combine(workspaceRoot, "DevPilot.sln");
        File.WriteAllText(devPilotSln, @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
# This is the DevPilot.sln file that should be ignored
VisualStudioVersion = 17.0.31903.59
MinimumVisualStudioVersion = 10.0.40219.1
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Release|Any CPU = Release|Any CPU
	EndGlobalSection
EndGlobal
");

        return workspaceRoot;
    }

    /// <summary>
    /// Creates workspace with Directory.Build.props for global coverlet.collector application.
    /// </summary>
    private string CreateWorkspaceWithDirectoryBuildProps()
    {
        var workspaceRoot = CreateSimpleCalculatorWorkspace();

        // Remove coverlet.collector from .csproj (it will come from Directory.Build.props)
        var testCsproj = Path.Combine(workspaceRoot, "Calculator.Tests", "Calculator.Tests.csproj");
        var csprojContent = File.ReadAllText(testCsproj);
        csprojContent = csprojContent.Replace(@"    <PackageReference Include=""coverlet.collector"" Version=""6.0.2"">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>", "");
        File.WriteAllText(testCsproj, csprojContent);

        // Add Directory.Build.props
        var directoryBuildProps = Path.Combine(workspaceRoot, "Directory.Build.props");
        File.WriteAllText(directoryBuildProps, @"
<Project>
  <!-- Global package references for test projects -->
  <ItemGroup Condition=""'$(IsPackable)' == 'false'"">
    <PackageReference Include=""coverlet.collector"" Version=""6.0.2"">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>
</Project>
");

        return workspaceRoot;
    }

    /// <summary>
    /// Creates workspace with compilation errors to test error handling.
    /// </summary>
    private string CreateWorkspaceWithCompilationError()
    {
        var workspaceRoot = CreateSimpleCalculatorWorkspace();

        // Introduce a compilation error
        var calculatorFile = Path.Combine(workspaceRoot, "Calculator", "Calculator.cs");
        File.WriteAllText(calculatorFile, @"
namespace Calculator;

public class Calculator
{
    public int Add(int a, int b) => a + b + UNDEFINED_VARIABLE; // This will fail to compile
}
");

        return workspaceRoot;
    }

    /// <summary>
    /// Creates workspace with no test projects.
    /// </summary>
    private string CreateWorkspaceWithNoTests()
    {
        var workspaceRoot = Path.Combine(_testBaseDirectory, "no-tests-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(workspaceRoot);
        _workspacesToCleanup.Add(workspaceRoot);

        // Create solution file
        var solutionFile = Path.Combine(workspaceRoot, "NoTests.sln");
        File.WriteAllText(solutionFile, @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
VisualStudioVersion = 17.0.31903.59
MinimumVisualStudioVersion = 10.0.40219.1
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""Calculator"", ""Calculator\Calculator.csproj"", ""{11111111-1111-1111-1111-111111111111}""
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Release|Any CPU = Release|Any CPU
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{11111111-1111-1111-1111-111111111111}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{11111111-1111-1111-1111-111111111111}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{11111111-1111-1111-1111-111111111111}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{11111111-1111-1111-1111-111111111111}.Release|Any CPU.Build.0 = Release|Any CPU
	EndGlobalSection
EndGlobal
");

        // Create main project (no tests)
        var mainDir = Path.Combine(workspaceRoot, "Calculator");
        Directory.CreateDirectory(mainDir);
        File.WriteAllText(Path.Combine(mainDir, "Calculator.csproj"), @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
");
        File.WriteAllText(Path.Combine(mainDir, "Calculator.cs"), @"
namespace Calculator;

public class Calculator
{
    public int Add(int a, int b) => a + b;
}
");

        return workspaceRoot;
    }

    #endregion

    #region Reflection Helpers (for testing private methods)

    /// <summary>
    /// Uses reflection to call TestRunner's private ParseCoverageFile method.
    /// </summary>
    private CoverageInfo? ParseCoverageFileViaReflection(string coverageFilePath)
    {
        var method = typeof(TestRunner).GetMethod("ParseCoverageFile",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        if (method == null)
        {
            throw new InvalidOperationException("ParseCoverageFile method not found via reflection");
        }

        return method.Invoke(null, new object[] { coverageFilePath }) as CoverageInfo;
    }

    #endregion

    public void Dispose()
    {
        foreach (var workspaceRoot in _workspacesToCleanup)
        {
            try
            {
                if (Directory.Exists(workspaceRoot))
                {
                    Directory.Delete(workspaceRoot, recursive: true);
                }
                else if (File.Exists(workspaceRoot))
                {
                    File.Delete(workspaceRoot);
                }
            }
            catch
            {
                // Ignore cleanup failures (file locks, etc.)
            }
        }

        try
        {
            if (Directory.Exists(_testBaseDirectory))
            {
                Directory.Delete(_testBaseDirectory, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup failures
        }
    }
}
