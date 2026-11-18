using System.Reflection;

namespace MaintainIQ.Tests;

public sealed class SolutionSetupTests
{
    [Fact]
    public void DomainDoesNotReferenceOtherMaintainIQLayers()
    {
        var domain = Assembly.Load("MaintainIQ.Domain");

        Assert.DoesNotContain(
            domain.GetReferencedAssemblies(),
            reference => reference.Name?.StartsWith("MaintainIQ.", StringComparison.Ordinal) == true);
    }
}
