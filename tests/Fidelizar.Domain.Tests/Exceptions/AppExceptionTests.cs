using Fidelizar.Domain.Exceptions;

namespace Fidelizar.Domain.Tests.Exceptions;

public class AppExceptionTests
{
    [Fact]
    public void WithDetail_accumulates_field_level_details()
    {
        var exception = new ValidationException()
            .WithDetail("monto", "required")
            .WithDetail("monto", "must be positive");

        Assert.Equal(2, exception.Details.Count);
        Assert.All(exception.Details, d => Assert.Equal("monto", d.Field));
    }

    [Fact]
    public void WithDetails_adds_every_tuple_in_order()
    {
        var exception = new ValidationException()
            .WithDetails([("a", "issue-a"), ("b", "issue-b")]);

        Assert.Collection(
            exception.Details,
            d => Assert.Equal(("a", "issue-a"), (d.Field, d.Issue)),
            d => Assert.Equal(("b", "issue-b"), (d.Field, d.Issue)));
    }

    [Fact]
    public void EntityNotFoundException_names_the_missing_entity_in_its_message()
    {
        var exception = new EntityNotFoundException("Miembro");

        Assert.Contains("Miembro", exception.Message);
        Assert.Equal("ENTITY_NOT_FOUND", exception.ErrorCode);
    }

    [Theory]
    [InlineData(typeof(ValidationException))]
    [InlineData(typeof(ConflictException))]
    [InlineData(typeof(AuthenticationException))]
    [InlineData(typeof(AuthorizationException))]
    public void Every_subtype_is_an_AppException(Type exceptionType)
    {
        Assert.True(typeof(AppException).IsAssignableFrom(exceptionType));
    }
}
