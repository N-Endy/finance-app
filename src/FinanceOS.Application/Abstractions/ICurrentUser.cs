namespace FinanceOS.Application.Abstractions;

public interface ICurrentUser
{
    Guid? UserId { get; }
    bool IsAuthenticated { get; }
    string? Email { get; }
    string? DisplayName { get; }
}
