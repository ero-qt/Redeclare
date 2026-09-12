using Implement;
using NUnit.Framework;
using System;
using System.Linq;

namespace Redeclare.Samples.Tests;

[TestFixture]
public sealed class ImplementGeneratorTests
{
    private const string Source = """
        namespace Config
        {
            public static partial class Settings
            {
                [Implement.FromEnvironment("HOME")] public static partial string? Home { get; }
                [Implement.FromEnvironment("SAMPLE", Settable = true)] public static partial string? Sample { get; set; }
                [Implement.Echo] public static partial void Greet(string who, int times);
                [Implement.Echo] internal static partial int Count();
            }
        }
        """;

    private static string Settings
    {
        get
        {
            return GeneratorRunner.Run<ImplementGenerator>(Source).Single(f => f.Contains("partial class Settings", StringComparison.Ordinal));
        }
    }

    [Test]
    public void Run_MinimalQualification_WritesTheUsingsTheFileNeeds()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Settings, Does.Contain("\nusing System;\n"));
            Assert.That(Settings, Does.Not.Contain("global::"));
        }
    }

    [Test]
    public void Run_PropertyDefinition_GetsAGetterReadingTheVariable()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Settings, Does.Contain("public static partial string? Home => Environment.GetEnvironmentVariable(\"HOME\");"));
            Assert.That(
                Settings,
                Does.Contain("public static partial string? Sample\n    {\n        get => Environment.GetEnvironmentVariable(\"SAMPLE\");"));
            Assert.That(Settings, Does.Contain("set => Environment.SetEnvironmentVariable(\"SAMPLE\", value);"));
        }
    }

    [Test]
    public void Run_MethodDefinition_GetsABodyEchoingItsArguments()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                Settings,
                Does.Contain(
                    "public static partial void Greet(string who, int times)\n    {\n"
                    + "        Console.WriteLine(\"Echo Greet\" + \", \" + who + \", \" + times);\n    }"));
            Assert.That(Settings, Does.Contain("internal static partial int Count()"), "the definition's accessibility and modifiers come along");
            Assert.That(Settings, Does.Contain("\"Echo Count\");\n        return default;"), "a non-void method gets a return");
        }
    }

    [Test]
    public void Run_SettableOnAGetterOnlyDefinition_FollowsTheDefinition()
    {
        const string GetterOnly = """
            public partial class Settings
            {
                [Implement.FromEnvironment("HOME", Settable = true)] public partial string? Home { get; }
            }
            """;

        var (files, errors) = GeneratorRunner.TryRun<ImplementGenerator>(GetterOnly);
        var settings = files.Single(f => f.Contains("partial class Settings", StringComparison.Ordinal));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settings, Does.Not.Contain("set"), "the definition has no setter, so the body for one goes nowhere");
            Assert.That(errors, Is.Empty);
        }
    }

    [Test]
    public void Run_SetterGivenNoBody_StaysAuto()
    {
        const string Unsettable = """
            public partial class Settings
            {
                [Implement.FromEnvironment("HOME")] public partial string? Home { get; set; }
            }
            """;

        var (files, errors) = GeneratorRunner.TryRun<ImplementGenerator>(Unsettable);
        var settings = files.Single(f => f.Contains("partial class Settings", StringComparison.Ordinal));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settings, Does.Contain("\n        set;\n"), "the setter comes along from the definition and keeps no body");
            Assert.That(errors, Is.Empty, "C# 13 backs an auto accessor next to a bodied one with a field");
        }
    }
}
