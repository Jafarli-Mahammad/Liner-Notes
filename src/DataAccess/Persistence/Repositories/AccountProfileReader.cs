using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.DTOs.Export;
using LinerNotes.DataAccess.DataContexts;
using Microsoft.EntityFrameworkCore;

namespace LinerNotes.DataAccess.Persistence.Repositories;

public sealed class AccountProfileReader(DataContext context) : IAccountProfileReader
{
    public Task<AccountExportDto?> GetAsync(Guid userId, CancellationToken cancellationToken = default) =>
        context.ApplicationUsers.AsNoTracking()
            .Where(account => account.Id == userId && !account.IsDeleted)
            .Select(account => new AccountExportDto(account.Id, account.UserName, account.NormalizedUserName,
                account.Email, account.NormalizedEmail, account.EmailConfirmed, account.PhoneNumber,
                account.PhoneNumberConfirmed, account.CreatedAt, account.CreatedBy, account.LastModifiedAt,
                account.LastModifiedBy, account.DeletedAt, account.DeletedBy, account.IsDeleted))
            .SingleOrDefaultAsync(cancellationToken);
}
