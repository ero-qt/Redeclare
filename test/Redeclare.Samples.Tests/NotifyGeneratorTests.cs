using Microsoft.CodeAnalysis;
using Notify;
using NUnit.Framework;
using System;
using System.Linq;

namespace Redeclare.Samples.Tests;

[TestFixture]
public sealed class NotifyGeneratorTests
{
    private const string Source = """
        using System.ComponentModel;

        namespace People
        {
            public sealed partial class Person
            {
                [Notify.Notify] private string _name = "";
                [Notify.Notify(PropertyName = "Years")] private int _age;
            }

            public partial class Account : INotifyPropertyChanged
            {
                [Notify.Notify] private decimal _balance;
                public event PropertyChangedEventHandler? PropertyChanged;
                protected virtual void OnPropertyChanged(string propertyName)
                    => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }

            public class NotPartial
            {
                [Notify.Notify] private int _ignored;
            }
        }
        """;

    private static string Person => Generated("partial class Person");

    private static string Account => Generated("partial class Account");

    private static string Generated(string marker)
    {
        return GeneratorRunner.Run<NotifyGenerator>(Source).Single(f => f.Contains(marker, StringComparison.Ordinal));
    }

    [Test]
    public void Run_MarkedFields_GenerateOneFilePerPartialClass()
    {
        var files = GeneratorRunner.Run<NotifyGenerator>(Source);

        Assert.That(files, Has.Count.EqualTo(3), "the attribute and one file per partial class, and the non-partial class is skipped");
    }

    [Test]
    public void Run_ClassWithoutThePlumbing_GetsInterfaceEventAndRaiser()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Person, Does.Contain("partial class Person : global::System.ComponentModel.INotifyPropertyChanged"));
            Assert.That(Person, Does.Contain("public event global::System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;"));
            Assert.That(Person, Does.Contain("private void OnPropertyChanged(string propertyName)"), "a sealed class gets a private raiser");
        }
    }

    [Test]
    public void Run_ClassWithThePlumbing_GetsOnlyTheProperty()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Account, Does.Contain("partial class Account\n{"), "the interface is already declared");
            Assert.That(Account, Does.Not.Contain("event"));
            Assert.That(Account, Does.Not.Contain("void OnPropertyChanged"));
            Assert.That(Account, Does.Contain("public decimal Balance"));
        }
    }

    [Test]
    public void Run_MarkedField_GetsAPropertyThatComparesBeforeRaising()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Person, Does.Contain("public string Name"));
            Assert.That(Person, Does.Contain("get => _name;"));
            Assert.That(Person, Does.Contain("if (global::System.Collections.Generic.EqualityComparer<string>.Default.Equals(_name, value))"));
            Assert.That(Person, Does.Contain("OnPropertyChanged(nameof(Years));"), "the attribute names the property");
        }
    }

    [Test]
    public void ReasonsAfterAnEdit_UnrelatedFile_AreCached()
    {
        var reasons = GeneratorRunner.ReasonsAfterAnEdit<NotifyGenerator>(Source, NotifyGenerator.FieldsStep);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reasons, Is.Not.Empty, "the step ran at least once");
            Assert.That(reasons, Has.All.EqualTo(IncrementalStepRunReason.Cached));
        }
    }

    [Test]
    public void ReasonsAfterAnEdit_AddedField_AreNotAllCached()
    {
        var reasons = GeneratorRunner.ReasonsAfterAnEdit<NotifyGenerator>(
            Source,
            NotifyGenerator.FieldsStep,
            Source.Replace(
                "[Notify.Notify(PropertyName = \"Years\")] private int _age;",
                "[Notify.Notify(PropertyName = \"Years\")] private int _age; [Notify.Notify] private int _added;",
                StringComparison.Ordinal));

        Assert.That(reasons, Has.Some.Not.EqualTo(IncrementalStepRunReason.Cached), "a real change reaches the step");
    }
}
