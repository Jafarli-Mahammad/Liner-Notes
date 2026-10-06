using FluentValidation.Results;
using LinerNotes.Application.Common.Exceptions;
using Xunit;

namespace LinerNotes.Application.Tests.Common.Exceptions;

public sealed class ExceptionTests
{
    [Fact]
    public void NotFoundException_WithEntityNameAndKey_FormatsMessageProperly()
    {
        var id = Guid.NewGuid();
        var ex = new NotFoundException("Track", id);

        Assert.Equal($"Entity \"Track\" ({id}) was not found.", ex.Message);
    }

    [Fact]
    public void NotFoundException_WithMessage_SetsMessageProperly()
    {
        var ex = new NotFoundException("Custom not found message");
        Assert.Equal("Custom not found message", ex.Message);
    }

    [Fact]
    public void ValidationException_DefaultConstructor_CreatesEmptyErrorsDictionary()
    {
        var ex = new ValidationException();
        Assert.NotNull(ex.Errors);
        Assert.Empty(ex.Errors);
    }

    [Fact]
    public void ValidationException_FromValidationFailures_GroupsErrorsByPropertyName()
    {
        var failures = new List<ValidationFailure>
        {
            new("Email", "Email is required."),
            new("Email", "Email must be a valid email address."),
            new("Password", "Password is required.")
        };

        var ex = new ValidationException(failures);

        Assert.Equal(2, ex.Errors.Count);
        Assert.True(ex.Errors.ContainsKey("Email"));
        Assert.True(ex.Errors.ContainsKey("Password"));
        Assert.Equal(2, ex.Errors["Email"].Length);
        Assert.Single(ex.Errors["Password"]);
    }
}
