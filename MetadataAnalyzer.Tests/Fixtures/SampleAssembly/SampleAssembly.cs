using System.Diagnostics.CodeAnalysis;

[assembly: System.Reflection.AssemblyMetadata("Fixture", "SampleAssembly")]

namespace MetadataAnalyzer.Fixtures;

public interface ISampleContract
{
    int GetValue(string name);

    bool IsActive { get; }
}

public enum SampleState
{
    None = 0,
    Started = 1,
    Finished = 2,
}

[Flags]
public enum SampleOptions : short
{
    None = 0,
    First = 1,
    Second = 2,
}

public delegate bool SampleHandler(object sender, int value);

public readonly struct SamplePoint(int x, int y)
{
    public int X { get; } = x;

    public int Y { get; } = y;

    public double Length => Math.Sqrt((X * X) + (Y * Y));
}

public abstract class SampleBase<T> : ISampleContract
{
    protected SampleBase(string name) => Name = name;

    public string Name { get; }

    public bool IsActive { get; private set; }

    public T? Payload { get; set; }

    public abstract int GetValue(string name);

    public virtual bool IsActiveValue() => IsActive;

    protected static int Count => 0;

    internal string Hidden { get; set; } = string.Empty;
}

public sealed class SampleService : SampleBase<int>
{
    private static readonly object Sync = new();

    private int _value;

    static SampleService() => Sync.ToString();

    public SampleService()
        : this("default")
    {
    }

    public SampleService(string name, int value = 5)
        : base(name) => _value = value;

    public override int GetValue(string name) => name.Length + _value;

    public bool IsActiveValue(string suffix = "!") => suffix.Length > 0;

    public void Configure(ref int counter, out string message, in SamplePoint point)
    {
        counter += point.X;
        message = $"{counter}";
    }

    public static SampleService Create() => new();

    public TResult Map<TResult>(Func<int, TResult> selector) => selector(_value);

    public event EventHandler? Changed;

    public class Nested
    {
        public int Depth { get; set; }
    }

    [SuppressMessage("Style", "IDE0051")]
    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
