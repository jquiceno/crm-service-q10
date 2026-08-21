using Infrastructure.Persistence.EntityFramework.LossReasons.Mappers;
using LossReason.Domain.Aggregates;
using Shouldly;
using Xunit;
using LossReasonDocument = Infrastructure.Persistence.EntityFramework.LossReasons.Entities.LossReason;

namespace UnitTests.Infrastructure.Persistence.LossReasons;

public sealed class LossReasonRepositoryMapperTests
{
    private const string ValidName = "Precio";

    [Fact]
    public void ToDomain_WithNullName_MapsToEmptyString()
    {
        var document = new LossReasonDocument
        {
            CauConsecutivoP = 3,
            CauNombre = null,
            CauEstado = true
        };

        var aggregate = LossReasonRepositoryMapper.ToDomain(document);

        aggregate.Name.ShouldBe(string.Empty);
        aggregate.Id.ShouldBe(3);
        aggregate.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void ToDomain_WithNullState_MapsToInactive()
    {
        var document = new LossReasonDocument
        {
            CauConsecutivoP = 4,
            CauNombre = ValidName,
            CauEstado = null
        };

        var aggregate = LossReasonRepositoryMapper.ToDomain(document);

        aggregate.IsActive.ShouldBeFalse();
        aggregate.Name.ShouldBe(ValidName);
    }

    [Fact]
    public void ToDomain_WithCompleteRow_MapsAllFields()
    {
        var document = new LossReasonDocument
        {
            CauConsecutivoP = 7,
            CauNombre = ValidName,
            CauEstado = true
        };

        var aggregate = LossReasonRepositoryMapper.ToDomain(document);

        aggregate.Id.ShouldBe(7);
        aggregate.Name.ShouldBe(ValidName);
        aggregate.IsActive.ShouldBeTrue();
        // Reconstruct does not validate nor stamp audit dates: the legacy table has no columns for them.
        aggregate.CreatedAt.ShouldBeNull();
        aggregate.UpdatedAt.ShouldBeNull();
    }

    [Fact]
    public void ToDocument_OnCreate_DoesNotSetIdentityColumn()
    {
        var result = LossReasonAggregate.Create(new CreateLossReasonArgs(ValidName, IsActive: true));
        result.IsSuccess.ShouldBeTrue();

        var document = LossReasonRepositoryMapper.ToDocument(result.Value);

        document.CauConsecutivoP.ShouldBe(0);
        document.CauNombre.ShouldBe(ValidName);
        document.CauEstado.ShouldBe(true);
    }
}
