using MaintainIQ.Domain.Common;
using MaintainIQ.Domain.Entities;
using MaintainIQ.Domain.Enums;

namespace MaintainIQ.Tests.Domain;

public sealed class EquipmentStateMachineTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Operational_CanBecomeFaulty()
    {
        var equipment = Create();

        equipment.MarkFaulty();

        Assert.Equal(EquipmentStatus.Faulty, equipment.Status);
    }

    [Fact]
    public void Operational_CanEnterMaintenance()
    {
        var equipment = Create();

        equipment.MarkUnderMaintenance();

        Assert.Equal(EquipmentStatus.UnderMaintenance, equipment.Status);
    }

    [Fact]
    public void Faulty_CanBecomeOperational()
    {
        var equipment = CreateFaulty();

        equipment.MarkOperational();

        Assert.Equal(EquipmentStatus.Operational, equipment.Status);
    }

    [Fact]
    public void Faulty_CanEnterMaintenance()
    {
        var equipment = CreateFaulty();

        equipment.MarkUnderMaintenance();

        Assert.Equal(EquipmentStatus.UnderMaintenance, equipment.Status);
    }

    [Fact]
    public void Maintenance_CanBecomeOperational()
    {
        var equipment = CreateUnderMaintenance();

        equipment.MarkOperational();

        Assert.Equal(EquipmentStatus.Operational, equipment.Status);
    }

    [Fact]
    public void Maintenance_CanBecomeFaulty()
    {
        var equipment = CreateUnderMaintenance();

        equipment.MarkFaulty();

        Assert.Equal(EquipmentStatus.Faulty, equipment.Status);
    }

    [Theory]
    [InlineData(EquipmentStatus.Operational)]
    [InlineData(EquipmentStatus.Faulty)]
    [InlineData(EquipmentStatus.UnderMaintenance)]
    public void ActiveStates_CanRetire(EquipmentStatus source)
    {
        var equipment = CreateInState(source);

        equipment.Retire();

        Assert.Equal(EquipmentStatus.Retired, equipment.Status);
    }

    [Theory]
    [InlineData("faulty")]
    [InlineData("maintenance")]
    [InlineData("operational")]
    [InlineData("retire")]
    public void Retired_IsTerminal(string action)
    {
        var equipment = Create();
        equipment.Retire();

        var error = Assert.Throws<BusinessRuleException>(() => Invoke(equipment, action));

        Assert.Equal("EQ-STATE", error.RuleCode);
        Assert.Equal(EquipmentStatus.Retired, equipment.Status);
    }

    [Fact]
    public void SameStateTransition_IsRejected()
    {
        var equipment = CreateFaulty();

        var error = Assert.Throws<BusinessRuleException>(equipment.MarkFaulty);

        Assert.Equal("EQ-STATE", error.RuleCode);
    }

    private static Equipment Create() => new("EQ-STATE", "Pompa", "Hat A", Now);

    private static Equipment CreateFaulty()
    {
        var equipment = Create();
        equipment.MarkFaulty();
        return equipment;
    }

    private static Equipment CreateUnderMaintenance()
    {
        var equipment = Create();
        equipment.MarkUnderMaintenance();
        return equipment;
    }

    private static Equipment CreateInState(EquipmentStatus status) => status switch
    {
        EquipmentStatus.Operational => Create(),
        EquipmentStatus.Faulty => CreateFaulty(),
        EquipmentStatus.UnderMaintenance => CreateUnderMaintenance(),
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    private static void Invoke(Equipment equipment, string action)
    {
        switch (action)
        {
            case "faulty":
                equipment.MarkFaulty();
                break;
            case "maintenance":
                equipment.MarkUnderMaintenance();
                break;
            case "operational":
                equipment.MarkOperational();
                break;
            case "retire":
                equipment.Retire();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action));
        }
    }
}
