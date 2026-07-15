using System.Collections.Generic;
using OyemCore.BusinessLayer.Dtos;
using OyemCore.DataLayer.Entities;

namespace OyemCore.BusinessLayer.Interfaces
{
    public interface ITicketService
    {
        (bool Success, object Data, string Message) InitConfig(int kullaniciID);
        (IEnumerable<object> Tickets, Dictionary<string, int> Counts) GetTicketList(int kullaniciID, string sirketKodu, string aramaText, int pageIndex, int pageSize);
        string SaveTicket(int kullaniciID, tb_Ticket ticket);
        bool UpdateTicketStatus(int kullaniciID, int ticketID, string yeniDurum, int? draggedID);
        bool AssignTicket(int kullaniciID, int ticketID, string sicilNo);
        object GetTicketDetail(int ticketID);
        IEnumerable<Company> GetCompanies();
        IEnumerable<object> GetCategories(string sirketKodu);
        IEnumerable<Personel> GetPersonels();
        bool SaveComment(int kullaniciID, int ticketID, string aciklama);
        bool SaveFile(int ticketID, string dosyaAdi, string dosyaYolu, string dosyaTipi);
        bool UpdateTicketSira(int kullaniciID, List<int> ticketIDs, string yeniDurum, int? draggedID);
        string DeleteTicket(int kullaniciID, int id, string webRootPath);
        object GetDashboardStats(int kullaniciID, string sirketKodu, int ay, int fltYil, int fltAy);
    }
}
