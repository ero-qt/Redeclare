using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace Redeclare.Tests;

/// <summary>
///     Packs the library and reads the result, so the layout a consumer compiles against is asserted rather than
///     assumed. The package is read straight out of the file: a restore would serve whatever version sits in the
///     global packages folder, which is the same 1.0.0 with older content.
/// </summary>
[TestFixture]
public sealed class PackagingTests
{
    private const string ContentRoot = "contentFiles/cs/netstandard2.0/Redeclare/";

    private const string NuspecNamespace = "http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd";

#if DEBUG
    private const string Configuration = "debug";
#else
    private const string Configuration = "release";
#endif

    private string _output = "";

    private List<string> _entries = [];

    private XElement _nuspec = null!;

    private static string RepositoryRoot
    {
        get
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Redeclare.slnx")))
                {
                    return directory.FullName;
                }
            }

            throw new InvalidOperationException($"No Redeclare.slnx above '{AppContext.BaseDirectory}'.");
        }
    }

    [OneTimeSetUp]
    public void Pack()
    {
        _output = Path.Combine(Path.GetTempPath(), "redeclare-pack-" + Guid.NewGuid().ToString("n")[..8]);

        var project = Path.Combine(RepositoryRoot, "src", "Redeclare", "Redeclare.csproj");
        var arguments = $"pack \"{project}\" --no-build --configuration {Configuration} --output \"{_output}\"";
        using var pack = Process.Start(new ProcessStartInfo("dotnet", arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        })!;

        var log = pack.StandardOutput.ReadToEnd() + pack.StandardError.ReadToEnd();
        pack.WaitForExit(milliseconds: 300_000);

        Assert.That(pack.ExitCode, Is.Zero, () => "dotnet pack failed:\n" + log);

        var package = Directory.EnumerateFiles(_output, "*.nupkg").Single();
        using var archive = ZipFile.OpenRead(package);
        _entries = [.. archive.Entries.Select(e => e.FullName)];

        using var nuspec = archive.GetEntry("Redeclare.nuspec")!.Open();
        _nuspec = XDocument.Load(nuspec).Root!.Element(XName.Get("metadata", NuspecNamespace))!;
    }

    [OneTimeTearDown]
    public void Clean()
    {
        if (Directory.Exists(_output))
        {
            Directory.Delete(_output, recursive: true);
        }
    }

    [Test]
    public void Pack_ContentFiles_HoldEverySourceFileAtItsOwnPath()
    {
        var source = Path.Combine(RepositoryRoot, "src", "Redeclare");
        var expected = Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories)
            .Select(f => ContentRoot + f[(source.Length + 1)..].Replace('\\', '/'))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        var actual = _entries.Where(e => e.StartsWith(ContentRoot, StringComparison.Ordinal))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void Pack_ContentFiles_AreCompiledByTheConsumer()
    {
        var files = _nuspec.Element(XName.Get("contentFiles", NuspecNamespace))!.Elements(XName.Get("files", NuspecNamespace)).ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(files, Is.Not.Empty);
            Assert.That(files.Select(f => f.Attribute("buildAction")!.Value), Has.All.EqualTo("Compile"));
        }
    }

    [Test]
    public void Pack_Package_ShipsTheBuildTargets()
    {
        Assert.That(_entries, Does.Contain("build/Redeclare.targets"));
    }

    [Test]
    public void Pack_Package_ShipsNoAssembly()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_entries.Where(e => e.StartsWith("lib/", StringComparison.Ordinal)), Is.Empty);
            Assert.That(_entries.Where(e => e.EndsWith(".dll", StringComparison.Ordinal)), Is.Empty);
        }
    }

    [Test]
    public void Pack_Package_IsADevelopmentDependencyThatCarriesNoDependencies()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_nuspec.Element(XName.Get("developmentDependency", NuspecNamespace))?.Value, Is.EqualTo("true"));
            Assert.That(
                _nuspec.Element(XName.Get("dependencies", NuspecNamespace))!.Descendants(XName.Get("dependency", NuspecNamespace)),
                Is.Empty,
                "Roslyn is the consumer's to reference");
        }
    }
}
