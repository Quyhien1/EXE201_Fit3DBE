using System.Threading.Tasks;

namespace Fit3d.BLL.Interfaces
{
    public interface IOtpService
    {
        Task<string> GenerateOtpAsync(string email);
        Task<bool> VerifyOtpAsync(string email, string code);
    }
}
