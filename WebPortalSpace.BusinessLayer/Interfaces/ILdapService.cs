namespace WebPortalSpace.BusinessLayer.Interfaces
{
    public interface ILdapService
    {
        (bool Success, string Email, string ErrorMessage) ValidateUser(string username, string password);
    }
}
