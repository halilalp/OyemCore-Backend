using System.Collections.Generic;

namespace OyemCore.BusinessLayer.Interfaces
{
    // referans: WebServiceChat (12 metot) — .NET 7 API portu.
    public interface IChatService
    {
        IEnumerable<object> GetUsers(int kullaniciID, bool onlyActive);
        IEnumerable<object> GetChatHistory(int kullaniciID, string targetSicilNo, int skip, int take);
        bool MarkConversationAsRead(int kullaniciID, string targetSicilNo);
        object GetMessageDetails(int kullaniciID, int messageID);
        object SaveMessage(int kullaniciID, string aliciSicilNo, string mesajMetni, string dosyaAdi, string dosyaYolu, string dosyaTipi, long dosyaBoyutu, int? parentID);
        object CreateGroup(int kullaniciID, string groupName, List<string> memberSicils);
        bool UpdateGroupMembers(int kullaniciID, string groupCode, List<string> memberSicils);
        object GetGroupDetails(int kullaniciID, string groupCode);
        object LeaveGroup(int kullaniciID, string groupCode);
        IEnumerable<object> GetSharedFiles(int kullaniciID, string targetSicilNo);
        int GetTotalUnreadCount(int kullaniciID);
        object ClearConversation(int kullaniciID, string targetSicilNo);
        object DeleteGroup(int kullaniciID, string groupCode);
        object GetActiveConnections();
    }
}
