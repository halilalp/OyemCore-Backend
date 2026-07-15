using System.Collections.Generic;
using OyemCore.DataLayer.Entities;

namespace OyemCore.BusinessLayer.Interfaces
{
    public interface IAdminService
    {
        IEnumerable<object> GetUsers(string search, string status);
        object GetUserDetail(int id);
        (bool Success, string Message, int? Id) SaveUser(int currentUserId, tb_Kullanici model);
        bool DeleteUser(int id);
        bool DeactivateUser(int id);
        IEnumerable<object> GetPersonnel();
        IEnumerable<object> GetProjects();
        (bool Success, string Message) SaveProject(tb_Proje model);
        (bool Success, string Message) DeleteProject(int id);
        bool SortProjects(List<int> sortedIds);
        IEnumerable<object> GetPages(int projectId);
        (bool Success, string Message) SavePage(tb_Sayfa model);
        (bool Success, string Message) DeletePage(int id);
        bool SortPages(List<int> sortedIds);
        List<int> GetPermissions(int userId);
        bool SavePermissions(int currentUserId, int userId, List<int> sayfaIds);
        object GetAdminDashboardStats();
        IEnumerable<object> GetLogs(string search);
        IEnumerable<object> GetSmsLogs(string search);
        IEnumerable<object> GetBelgeTarihce(string search);
        IEnumerable<object> GetAiSettings();
        (bool Success, string Message) SaveAiSetting(tb_AiAyarlar model);
        IEnumerable<object> GetTicketCategories();
        (bool Success, string Message) SaveTicketCategory(tb_TicketKategori model);
        bool DeleteTicketCategory(int id);
        IEnumerable<object> GetHierarchy();
        (bool Success, string Message) SaveHierarchy(tb_Hiyerarsi model);
        bool DeleteHierarchy(int id);

        // Mobile Admin Settings extensions
        (bool Success, string Message) UpdateUserPassword(int id, string newPassword);
        IEnumerable<object> GetUserDocumentTypes(int userId);
        (bool Success, string Message) SaveUserDocumentTypes(int userId, List<string> codes);
        IEnumerable<object> GetHelpDeskCategories(string search, string categoryId, string typeCode);
        object GetHelpDeskCategoryDetail(int id);
        (bool Success, string Message) SaveHelpDeskCategory(tb_TalepKategori model);
        bool DeleteHelpDeskCategory(int id);
        (bool Success, string Message) SaveCategoryResponsible(tb_TalepAyar model);
        bool DeleteCategoryResponsible(int id);
        IEnumerable<object> GetHelpDeskTypes();
        IEnumerable<object> GetCompanies();
        (IEnumerable<object> Items, int TotalCount) GetLogsPaged(string search, string userEmail, DateTime? startDate, DateTime? endDate, int page, int pageSize);
        (IEnumerable<object> Items, int TotalCount) GetBelgeTarihcePaged(string search, string documentCode, DateTime? startDate, DateTime? endDate, int page, int pageSize);
    }
}
