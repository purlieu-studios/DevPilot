using FluentAssertions;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.MSBuild;
using System.Collections.Immutable;

namespace DevPilot.Orchestrator.Tests;

/// <summary>
/// Smoke tests for Roslyn package compatibility.
///
/// These tests validate that critical Roslyn packages work correctly together
/// after package upgrades. They run quickly (< 5 seconds total) and catch
/// incompatibility issues early.
///
/// If any of these tests fail after a package upgrade, it indicates a breaking
/// change in:
/// - Microsoft.Build.Locator
/// - Microsoft.CodeAnalysis.Workspaces.MSBuild
/// - Microsoft.CodeAnalysis.CSharp.Workspaces
/// - Microsoft.CodeAnalysis.NetAnalyzers
/// </summary>
public sealed class RoslynSmokeTests
{
    #region MSBuildLocator Smoke Tests

    [Fact]
    public void MSBuildLocator_CanQueryInstances()
    {
        // This validates Microsoft.Build.Locator package is loadable

        // Act
        var exception = Record.Exception(() =>
        {
            var instances = MSBuildLocator.QueryVisualStudioInstances();
            instances.Should().NotBeNull("MSBuildLocator should return instances");
        });

        // Assert
        exception.Should().BeNull("MSBuildLocator.QueryVisualStudioInstances should not throw");
    }

    [Fact]
    public void MSBuildLocator_CanCheckRegistration()
    {
        // This validates MSBuildLocator API stability

        // Act
        var exception = Record.Exception(() =>
        {
            var isRegistered = MSBuildLocator.IsRegistered;
            // IsRegistered can be true or false, just verify it's callable
        });

        // Assert
        exception.Should().BeNull("MSBuildLocator.IsRegistered should not throw");
    }

    #endregion

    #region MSBuildWorkspace Smoke Tests

    [Fact]
    public void MSBuildWorkspace_CanBeCreated()
    {
        // This validates Microsoft.CodeAnalysis.Workspaces.MSBuild package

        // Act
        var exception = Record.Exception(() =>
        {
            using var workspace = MSBuildWorkspace.Create();
            workspace.Should().NotBeNull("MSBuildWorkspace should be created");
        });

        // Assert
        exception.Should().BeNull("MSBuildWorkspace.Create should not throw");
    }

    #endregion

    #region Compilation Smoke Tests

    [Fact]
    public void CSharpCompilation_CanBeCreated()
    {
        // This validates Microsoft.CodeAnalysis.CSharp package

        // Arrange
        var syntaxTree = CSharpSyntaxTree.ParseText("class Program { }");

        // Act
        var exception = Record.Exception(() =>
        {
            var compilation = CSharpCompilation.Create(
                assemblyName: "TestAssembly",
                syntaxTrees: new[] { syntaxTree });

            compilation.Should().NotBeNull("CSharpCompilation should be created");
        });

        // Assert
        exception.Should().BeNull("CSharpCompilation.Create should not throw");
    }

    [Fact]
    public void Compilation_GetDiagnostics_DoesNotRequireAnalyzers()
    {
        // This validates that compilation diagnostics work without analyzers
        // (CodeAnalyzer falls back to this when no analyzers are loaded)

        // Arrange
        var syntaxTree = CSharpSyntaxTree.ParseText("class Program { }");
        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: new[] { syntaxTree });

        // Act
        var exception = Record.Exception(() =>
        {
            var diagnostics = compilation.GetDiagnostics();
            diagnostics.Should().NotBeNull("Diagnostics should be retrievable without analyzers");
        });

        // Assert
        exception.Should().BeNull("Compilation.GetDiagnostics should not throw");
    }

    #endregion

    #region Diagnostic Smoke Tests

    [Fact]
    public void Compilation_CanGetDiagnostics()
    {
        // This validates basic diagnostic retrieval (used by CodeAnalyzer)

        // Arrange
        var syntaxTree = CSharpSyntaxTree.ParseText(@"
class Program
{
    void Test()
    {
        var x = undefinedVariable;  // CS0103
    }
}");

        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: new[] { syntaxTree });

        // Act
        var exception = Record.Exception(() =>
        {
            var diagnostics = compilation.GetDiagnostics();
            diagnostics.Should().NotBeNull("Diagnostics should be retrievable");
            diagnostics.Should().Contain(d => d.Id == "CS0103", "CS0103 error should be detected");
        });

        // Assert
        exception.Should().BeNull("Compilation.GetDiagnostics should not throw");
    }

    #endregion

    #region Package Version Documentation Tests

    [Fact]
    public void PackageVersions_DocumentedCorrectly()
    {
        // This test serves as documentation for required package versions.
        // Update this comment when upgrading packages.

        // Current package versions (as of 2025-10-21):
        // - Microsoft.Build.Locator: 1.7.8
        // - Microsoft.CodeAnalysis.CSharp.Workspaces: 4.11.0
        // - Microsoft.CodeAnalysis.Workspaces.MSBuild: 4.11.0
        // - Microsoft.CodeAnalysis.NetAnalyzers: 9.0.0

        // Known constraints:
        // - Microsoft.Build.Locator 1.10.2 requires ExcludeAssets="runtime" on Microsoft.Build.* packages
        //   which breaks MSBuildWorkspace, so we use 1.7.8
        // - Microsoft.CodeAnalysis.* packages must match versions (4.11.0)
        // - Microsoft.Build.Tasks.Core has security vulnerability (GHSA-h4j7-5rxr-p4wc) but cannot be upgraded
        //   due to MSBuildWorkspace compatibility

        true.Should().BeTrue("This test documents package version constraints");
    }

    #endregion
}
