using Xunit;

namespace HeavyIMS.Tests.IntegrationTests
{
    /// <summary>
    /// Collection definition to ensure integration tests run sequentially
    /// This prevents tests from interfering with each other when using a shared database
    /// </summary>
    [CollectionDefinition("Database Collection", DisableParallelization = true)]
    public class DatabaseCollection
    {
        // This class is never instantiated
    }
}
