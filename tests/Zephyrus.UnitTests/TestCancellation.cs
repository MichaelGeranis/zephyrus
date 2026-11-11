namespace Zephyrus.UnitTests;

/// <summary>
/// The running test's cancellation token, so async calls stop promptly when a
/// test is cancelled or times out (xUnit1051). Imported everywhere through a
/// global <c>using static</c>, so a call reads <c>AddAsync(feature, Ct)</c>.
/// </summary>
internal static class TestCancellation
{
    public static CancellationToken Ct => TestContext.Current.CancellationToken;
}
