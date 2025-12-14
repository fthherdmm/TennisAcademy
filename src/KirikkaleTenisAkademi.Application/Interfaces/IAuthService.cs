using KirikkaleTenisAkademi.Application.DTOs.Auth;

namespace KirikkaleTenisAkademi.Application.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResponse> LoginAsync(LoginRequest request);
        Task<AuthResponse> RegisterAsync(RegisterRequest request);
    }
}