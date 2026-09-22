using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace PayMaestro.Tests.Support;

/// <summary>An environment with a chosen name, for code that behaves differently outside Development.</summary>
public sealed class StubHostEnvironment : IHostEnvironment
{
    public StubHostEnvironment(string environmentName)
    {
        EnvironmentName = environmentName;
    }

    public string EnvironmentName { get; set; }

    public string ApplicationName { get; set; } = "PayMaestro.Tests";

    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
