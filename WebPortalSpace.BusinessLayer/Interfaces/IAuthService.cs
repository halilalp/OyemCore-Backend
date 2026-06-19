using System;

namespace WebPortalSpace.BusinessLayer.Interfaces
{
    public interface IAuthService
    {
        (bool Success, string Token, string Message) Authenticate(string username, string password, string ipAddress, string userAgent);
        (bool Success, string Message) ResetPassword(string sicilNo, string username);
        void SavePushToken(int kullaniciID, string token);
        void ClearPushToken(int kullaniciID);
    }
}
