using System.Collections.Generic;
using WebPortalSpace.DataLayer.Entities;
using WebPortalSpace.BusinessLayer.Dtos;

namespace WebPortalSpace.BusinessLayer.Interfaces
{
    public interface ITalepService
    {
        IEnumerable<object> GetRequests(int kullaniciID, string turKodu);
        IEnumerable<tb_TalepKategori> GetCategories(string turKodu);
        object GetRequestDetail(int kullaniciID, int talepID);
        string SaveRequest(int kullaniciID, tb_Talep request, tb_TalepBakim bakim = null);
        bool UpdateRequestStatus(int kullaniciID, int talepID, string status);
        bool AssignRequest(int kullaniciID, int talepID, string sicilNo);
        bool AddRequestGelisme(int kullaniciID, int talepID, string aciklama);
        IEnumerable<Personel> GetPersonels(string tur);
        bool ToggleRequestLock(int kullaniciID, int talepID);
        bool SendRequestForApproval(int kullaniciID, int talepID, string amirSicil);
        bool RetractRequestApproval(int kullaniciID, int talepID);
        bool ApproveOrRejectRequest(int kullaniciID, int talepID, bool approve, string comment);
        bool AskQuestionToPersonnel(int kullaniciID, int talepID, string targetSicil, string questionText);
        bool AddHelperPersonnel(int kullaniciID, int talepID, string helperSicil);
        bool DeleteHelperPersonnel(int kullaniciID, int talepID, string helperSicil);
        IEnumerable<Personel> GetAllActivePersonel();
    }
}
