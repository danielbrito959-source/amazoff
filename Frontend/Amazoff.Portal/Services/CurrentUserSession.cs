using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace Amazoff.Portal.Services;

public sealed class CurrentUserSession(ProtectedSessionStorage sessionStorage)
{
    private const string CurrentUserKey = "current-user";

    public async Task SetAsync(CurrentUserDto user)
    {
        await sessionStorage.SetAsync(CurrentUserKey, user);
    }

    public async Task<CurrentUserDto?> GetAsync()
    {
        var result = await sessionStorage.GetAsync<CurrentUserDto>(CurrentUserKey);
        return result.Success ? result.Value : null;
    }

    public async Task ClearAsync()
    {
        await sessionStorage.DeleteAsync(CurrentUserKey);
    }
}

public sealed record CurrentUserDto(
    int Id,
    string Username,
    string Email,
    int? RoleId,
    string FirstName,
    string LastName);
