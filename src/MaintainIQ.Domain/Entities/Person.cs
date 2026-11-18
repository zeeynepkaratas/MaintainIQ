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
