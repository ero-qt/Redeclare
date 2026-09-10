using System;
using System.ComponentModel;

namespace Notify.Consumer;

/// <summary>
///     Two fields become two properties. The interface, the event and the raise method are generated too.
/// </summary>
public sealed partial class Person
{
    [Notify]
    private string _name = "";

    [Notify(PropertyName = "Years")]
    private int _age;
}

/// <summary>
///     Already implements the interface and raises its own event, so only the property is generated.
/// </summary>
public partial class Account : INotifyPropertyChanged
{
    [Notify]
    private decimal _balance;

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    ///     Raises <see cref="PropertyChanged"/>.
    /// </summary>
    /// <param name="propertyName">The property that changed.</param>
    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
///     Uses the generated members, so this project only compiles when the generator ran.
/// </summary>
public static class Demo
{
    /// <summary>
    ///     Counts the changes a rename raises.
    /// </summary>
    /// <param name="person">The person to rename.</param>
    /// <param name="name">The new name.</param>
    /// <returns>How many times the name was reported changed.</returns>
    public static int Rename(Person person, string name)
    {
        ArgumentNullException.ThrowIfNull(person);

        int raised = 0;
        person.PropertyChanged += (_, e) => raised += e.PropertyName == nameof(Person.Name) ? 1 : 0;
        person.Name = name;
        person.Years = 30;

        return raised;
    }
}
