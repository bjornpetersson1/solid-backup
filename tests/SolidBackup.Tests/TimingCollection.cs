namespace SolidBackup.Tests;

/// <summary>
/// Tests that measure how long something takes. They run alone: with other test classes running in parallel they
/// would measure the others' load, not the code under test.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class TimingCollection
{
    public const string Name = "Timing";
}
