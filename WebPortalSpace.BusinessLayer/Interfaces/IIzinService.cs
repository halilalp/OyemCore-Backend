using System.Collections.Generic;
using WebPortalSpace.DataLayer.Entities;

namespace WebPortalSpace.BusinessLayer.Interfaces
{
    public interface IIzinService
    {
        (IEnumerable<object> Requests, int YillikIzinBalance) GetIzinRequests(int kullaniciID);
        IEnumerable<object> GetIzinApprovals(int kullaniciID);
        bool SaveIzinRequest(int kullaniciID, tb_IzinOnay request);
        bool ApproveIzinRequest(int kullaniciID, int izinOnayID);
        bool RejectIzinRequest(int kullaniciID, int izinOnayID);
        IEnumerable<object> GetIzinHistory(string belgeNo);
    }
}
