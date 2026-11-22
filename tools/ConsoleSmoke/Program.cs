using MaintainIQ.Data.Connection;
using MaintainIQ.Data.Repositories;
using MaintainIQ.Data.Transactions;

var connectionString = args.Length > 0
    ? args[0]
    : "Server=.\\SQLEXPRESS;Database=MaintainIQ;Trusted_Connection=True;TrustServerCertificate=True";

var factory = new SqlConnectionFactory(connectionString);
await using var unitOfWork = new UnitOfWork(factory);
await unitOfWork.BeginAsync();

var checks = new (string Name, Func<Task<int>> Count)[]
{
    ("Equipment", async () => (await new EquipmentRepository(unitOfWork).GetAllAsync()).Count),
    ("Failures", async () => (await new FailureRepository(unitOfWork).GetAllAsync()).Count),
    ("Work orders", async () => (await new WorkOrderRepository(unitOfWork).GetAllAsync()).Count),
    ("Spare parts", async () => (await new SparePartRepository(unitOfWork).GetAllAsync()).Count),
    ("Stock movements", async () => (await new StockMovementRepository(unitOfWork).GetAllAsync()).Count),
    ("Maintenance plans", async () => (await new MaintenancePlanRepository(unitOfWork).GetAllAsync()).Count),
    ("Technicians", async () => (await new TechnicianRepository(unitOfWork).GetAllAsync()).Count),
    ("Users", async () => (await new UserRepository(unitOfWork).GetAllAsync()).Count),
    ("Attachments", async () => (await new AttachmentRepository(unitOfWork).GetAllAsync()).Count),
    ("Audit records", async () => (await new AuditRepository(unitOfWork).GetAllAsync()).Count),
};

Console.WriteLine("MaintainIQ repository smoke check");
foreach (var check in checks)
{
    Console.WriteLine($"{check.Name,-20}: {await check.Count(),5}");
}

Console.WriteLine($"{"Equipment tree",-20}: {(await new EquipmentRepository(unitOfWork).GetTreeAsync()).Count,5}");
await unitOfWork.RollbackAsync();
Console.WriteLine("Smoke check completed successfully.");
