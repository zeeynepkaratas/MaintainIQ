namespace MaintainIQ.Data.Common;

public sealed class ConcurrencyException(string message) : Exception(message);

public sealed class UnitOfWorkException(string message) : InvalidOperationException(message);
