using MaintainIQ.Domain.Common;

namespace MaintainIQ.Domain.Entities;

public abstract class Person : BaseEntity
{
    protected Person(
        string employeeNumber,
        string firstName,
        string lastName,
        DateTimeOffset createdAt)
        : base(createdAt)
    {
        EmployeeNumber = Guard.Required(employeeNumber, "Personel numarası", 30);
        FirstName = Guard.Required(firstName, "Ad", 80);
        LastName = Guard.Required(lastName, "Soyad", 80);
    }

    public string EmployeeNumber { get; private set; }

    public string FirstName { get; private set; }

    public string LastName { get; private set; }

    public string FullName => $"{FirstName} {LastName}";

    public bool IsActive { get; private set; } = true;

    internal void RestorePersonState(
        int id, bool isActive, DateTimeOffset createdAt, DateTimeOffset updatedAt, byte[] rowVersion)
    {
        IsActive = isActive;
        RestorePersistenceState(id, createdAt, updatedAt, rowVersion);
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}

public sealed class Technician : Person
{
    public Technician(
        string employeeNumber,
        string firstName,
        string lastName,
        DateTimeOffset createdAt,
        string? specialty = null)
        : base(employeeNumber, firstName, lastName, createdAt)
    {
        Specialty = Guard.Optional(specialty, "Uzmanlık", 120);
    }

    public string? Specialty { get; private set; }

    internal static Technician Rehydrate(
        int id, string employeeNumber, string firstName, string lastName, string? specialty,
        bool isActive, DateTimeOffset createdAt, DateTimeOffset updatedAt, byte[] rowVersion)
    {
        var technician = new Technician(employeeNumber, firstName, lastName, createdAt, specialty);
        technician.RestorePersonState(id, isActive, createdAt, updatedAt, rowVersion);
        return technician;
    }
}

public sealed class Supervisor : Person
{
    public Supervisor(string employeeNumber, string firstName, string lastName, DateTimeOffset createdAt)
        : base(employeeNumber, firstName, lastName, createdAt)
    {
    }
}

public sealed class AdminPerson : Person
{
    public AdminPerson(string employeeNumber, string firstName, string lastName, DateTimeOffset createdAt)
        : base(employeeNumber, firstName, lastName, createdAt)
    {
    }
}

public sealed class OperatorPerson : Person
{
    public OperatorPerson(string employeeNumber, string firstName, string lastName, DateTimeOffset createdAt)
        : base(employeeNumber, firstName, lastName, createdAt)
    {
    }
}
