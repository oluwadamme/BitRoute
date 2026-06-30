using System.Reflection;
using NetArchTest.Rules;

namespace BitRoute.ArchitectureTests;

/// <summary>
/// Enforces the Clean Architecture rule that dependencies always point inward:
/// Api -> Application -> Domain, and Infrastructure -> Domain. Nothing points outward.
/// These tests fail the build the moment a layer references one it must not see.
/// </summary>
public class DependencyDirectionTests
{
    private const string Domain = "BitRoute.Domain";
    private const string Application = "BitRoute.Application";
    private const string Infrastructure = "BitRoute.Infrastructure";
    private const string Api = "BitRoute.Api";

    [Fact]
    public void Domain_Should_Not_DependOnAnyOtherLayer()
    {
        var result = Types.InAssembly(LayerAssembly(Domain))
            .Should()
            .NotHaveDependencyOnAny(Application, Infrastructure, Api)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Application_Should_Not_DependOnInfrastructureOrApi()
    {
        var result = Types.InAssembly(LayerAssembly(Application))
            .Should()
            .NotHaveDependencyOnAny(Infrastructure, Api)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Infrastructure_Should_Not_DependOnApi()
    {
        var result = Types.InAssembly(LayerAssembly(Infrastructure))
            .Should()
            .NotHaveDependencyOn(Api)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    private static Assembly LayerAssembly(string name) => Assembly.Load(new AssemblyName(name));

    private static string Describe(TestResult result) =>
        result.IsSuccessful
            ? string.Empty
            : $"The following types violate the dependency-direction rule: {string.Join(", ", result.FailingTypeNames)}";
}
