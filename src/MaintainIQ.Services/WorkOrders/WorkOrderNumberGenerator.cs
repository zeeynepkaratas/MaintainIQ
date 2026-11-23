namespace MaintainIQ.Services.WorkOrders;

public interface IWorkOrderNumberGenerator
{
    string Next(DateTimeOffset now);
}

public sealed class WorkOrderNumberGenerator : IWorkOrderNumberGenerator
{
    public string Next(DateTimeOffset now) =>
        $"WO-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..25].ToUpperInvariant();
}
