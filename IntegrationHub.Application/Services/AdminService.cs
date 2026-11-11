using IntegrationHub.Application.Contracts;
using IntegrationHub.Application.DTOs;
using IntegrationHub.Models.Entities;

namespace IntegrationHub.Application.Services;

public enum AdminOperationStatus
{
    Success,
    NotFound,
    Conflict
}

public sealed record AdminOperationResult<T>(
    AdminOperationStatus Status,
    T? Value = default,
    string? Error = null);

public sealed class AdminService
{
    private const string UserNotFound = "Kullanıcı bulunamadı.";
    private const string EmailAlreadyRegistered = "Bu e-posta zaten kayıtlı.";

    private readonly IUserRepository _users;
    private readonly IUserActivityRepository _activities;
    private readonly IPasswordHasher _passwordHasher;

    public AdminService(
        IUserRepository users,
        IUserActivityRepository activities,
        IPasswordHasher passwordHasher)
    {
        _users = users;
        _activities = activities;
        _passwordHasher = passwordHasher;
    }

    public async Task<List<AdminUserResponse>> GetUsersAsync(CancellationToken ct)
    {
        var users = await _users.GetAllAsync(ct);
        return users.Select(ToResponse).ToList();
    }

    public async Task<AdminOperationResult<AdminUserResponse>> GetUserByIdAsync(int id, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(id, ct);
        return user is null ? NotFound<AdminUserResponse>(UserNotFound) : Success(ToResponse(user));
    }

    public async Task<AdminOperationResult<AdminUserResponse>> CreateUserAsync(
        CreateAdminUserRequest request,
        CancellationToken ct)
    {
        if (await _users.EmailExistsAsync(request.Email, null, ct))
        {
            return Conflict<AdminUserResponse>(EmailAlreadyRegistered);
        }

        var user = new User
        {
            Email = request.Email,
            Username = request.Username,
            PasswordHash = _passwordHasher.Hash(request.Password),
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
        await _users.AddAsync(user, ct);
        return Success(ToResponse(user));
    }

    public async Task<AdminOperationResult<AdminUserResponse>> UpdateUserAsync(
        int id,
        UpdateAdminUserRequest request,
        bool isCurrentUser,
        CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(id, ct);
        if (user is null)
        {
            return NotFound<AdminUserResponse>(UserNotFound);
        }

        if (await _users.EmailExistsAsync(request.Email, id, ct))
        {
            return Conflict<AdminUserResponse>(EmailAlreadyRegistered);
        }

        if (isCurrentUser && (!request.IsAdmin || !request.IsActive))
        {
            return Conflict<AdminUserResponse>(
                "Kendi yönetici hesabınızı devre dışı bırakamaz veya yetkisini kaldıramazsınız.");
        }

        if (user.IsAdmin && user.IsActive && (!request.IsAdmin || !request.IsActive) &&
            await _users.CountActiveAdminsAsync(ct) <= 1)
        {
            return Conflict<AdminUserResponse>("Sistemde en az bir etkin yönetici bulunmalıdır.");
        }

        user.Email = request.Email;
        user.Username = request.Username;
        user.IsActive = request.IsActive;
        user.IsAdmin = request.IsAdmin;
        await _users.SaveChangesAsync(ct);
        return Success(ToResponse(user));
    }

    public async Task<AdminOperationResult<bool>> SetPasswordAsync(
        int id,
        SetAdminUserPasswordRequest request,
        CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(id, ct);
        if (user is null)
        {
            return NotFound<bool>(UserNotFound);
        }

        user.PasswordHash = _passwordHasher.Hash(request.Password);
        await _users.SaveChangesAsync(ct);
        return Success(true);
    }

    public async Task<AdminOperationResult<List<UserActivityDto>>> GetHistoryAsync(int id, CancellationToken ct)
    {
        if (await _users.GetByIdAsync(id, ct) is null)
        {
            return NotFound<List<UserActivityDto>>(UserNotFound);
        }

        var activities = await _activities.GetHistoryAsync(id, 200, ct);
        return Success(activities.Select(ToDto).ToList());
    }

    public async Task<AdminOperationResult<bool>> ClearHistoryAsync(int id, CancellationToken ct)
    {
        if (await _users.GetByIdAsync(id, ct) is null)
        {
            return NotFound<bool>(UserNotFound);
        }

        await _activities.DeleteByUserAsync(id, ct);
        return Success(true);
    }

    public async Task<AdminOperationResult<bool>> DeleteUserAsync(
        int id,
        bool isCurrentUser,
        CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(id, ct);
        if (user is null)
        {
            return NotFound<bool>(UserNotFound);
        }

        if (isCurrentUser)
        {
            return Conflict<bool>("Kendi hesabınızı silemezsiniz.");
        }

        if (user.IsAdmin && user.IsActive && await _users.CountActiveAdminsAsync(ct) <= 1)
        {
            return Conflict<bool>("Sistemde en az bir etkin yönetici bulunmalıdır.");
        }

        await _activities.DeleteByUserAsync(id, ct);
        _users.Remove(user);
        await _users.SaveChangesAsync(ct);
        return Success(true);
    }

    private static AdminOperationResult<T> Success<T>(T value) =>
        new(AdminOperationStatus.Success, value);

    private static AdminOperationResult<T> NotFound<T>(string error) =>
        new(AdminOperationStatus.NotFound, Error: error);

    private static AdminOperationResult<T> Conflict<T>(string error) =>
        new(AdminOperationStatus.Conflict, Error: error);

    private static AdminUserResponse ToResponse(User user) => new()
    {
        Id = user.Id,
        Username = user.Username,
        Email = user.Email,
        IsActive = user.IsActive,
        IsAdmin = user.IsAdmin,
        CreatedAt = user.CreatedAt
    };

    private static UserActivityDto ToDto(UserActivity activity) => new()
    {
        Id = activity.Id,
        ServiceType = activity.ServiceType,
        SubCategory = activity.SubCategory,
        InputLabel = activity.InputLabel,
        InputValue = activity.InputValue,
        ResultLabel = activity.ResultLabel,
        ResultValue = activity.ResultValue,
        Timestamp = activity.Timestamp
    };
}
