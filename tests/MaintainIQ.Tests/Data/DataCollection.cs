namespace MaintainIQ.Tests.Data;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DataCollection : ICollectionFixture<DataFixture>
{
    public const string Name = "SQL Server data tests";
}

public sealed class DataFixture
{
    public const string ConnectionString =
        "Server=.\\SQLEXPRESS;Database=MaintainIQ;Trusted_Connection=True;TrustServerCertificate=True";
}
