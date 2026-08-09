using System;
using System.Collections.Generic;
using System.Linq;
using OyemCore.BusinessLayer.Interfaces;
using OyemCore.DataLayer.Entities;
using OyemCore.DataLayer.Interfaces;

namespace OyemCore.BusinessLayer.Services
{
    // Uygulama-içi bildirim merkezi. referans: WebServiceBildirim (birebir).
    public class BildirimService : IBildirimService
    {
        private readonly IYbsDbContext _context;

        public BildirimService(IYbsDbContext context)
        {
            _context = context;
        }

        // Giriş yapan kullanıcının sicilNo'sunu çözer (referans: GetCurrentSicilNo).
        private string GetCurrentSicilNo(int kullaniciID)
        {
            var user = _context.tb_Kullanici.FirstOrDefault(u => u.KullaniciID == kullaniciID);
            return user?.SicilNo;
        }

        public (int total, IEnumerable<object> data) GetNotifications(int kullaniciID, int pageIndex, int pageSize)
        {
            string sicilNo = GetCurrentSicilNo(kullaniciID);
            if (string.IsNullOrEmpty(sicilNo)) return (0, new List<object>());

            int skip = pageIndex * pageSize;

            int totalCount = _context.tb_Notification.Count(n => n.SicilNo == sicilNo);

            var list = _context.tb_Notification
                .Where(n => n.SicilNo == sicilNo)
                .OrderByDescending(n => n.KayitTarihi)
                .Skip(skip)
                .Take(pageSize)
                .Select(n => new
                {
                    n.ID,
                    n.SicilNo,
                    n.Baslik,
                    n.Aciklama,
                    n.LinkUrl,
                    n.Kategori,
                    n.ReferansID,
                    n.Okundu,
                    n.KayitTarihi
                })
                .ToList();

            return (totalCount, list);
        }

        public bool MarkAsRead(int kullaniciID, int notificationId)
        {
            string sicilNo = GetCurrentSicilNo(kullaniciID);
            if (string.IsNullOrEmpty(sicilNo)) return false;

            var notif = _context.tb_Notification.FirstOrDefault(n => n.ID == notificationId && n.SicilNo == sicilNo);
            if (notif != null)
            {
                notif.Okundu = true;
                _context.SaveChanges();
            }
            return true;
        }

        public bool MarkAllAsRead(int kullaniciID)
        {
            string sicilNo = GetCurrentSicilNo(kullaniciID);
            if (string.IsNullOrEmpty(sicilNo)) return false;

            var unreadList = _context.tb_Notification.Where(n => n.SicilNo == sicilNo && n.Okundu == false).ToList();
            foreach (var notif in unreadList)
            {
                notif.Okundu = true;
            }
            _context.SaveChanges();
            return true;
        }

        public bool DeleteNotification(int kullaniciID, int notificationId)
        {
            string sicilNo = GetCurrentSicilNo(kullaniciID);
            if (string.IsNullOrEmpty(sicilNo)) return false;

            var notif = _context.tb_Notification.FirstOrDefault(n => n.ID == notificationId && n.SicilNo == sicilNo);
            if (notif != null)
            {
                _context.tb_Notification.Remove(notif);
                _context.SaveChanges();
            }
            return true;
        }

        public int GetUnreadCount(int kullaniciID)
        {
            string sicilNo = GetCurrentSicilNo(kullaniciID);
            if (string.IsNullOrEmpty(sicilNo)) return 0;

            return _context.tb_Notification.Count(n => n.SicilNo == sicilNo && n.Okundu == false);
        }

        // referans: WebServiceBildirim.AddNotification — diğer modüller çağırır.
        // Push gönderimi API'de ilgili event noktalarında IPushNotificationService ile
        // ayrıca yapılıyor; burada uygulama-içi bildirim kaydı oluşturulur.
        // (SignalR anlık dağıtımı chat/hub fazında bağlanacak.)
        public void AddNotification(string sicilNo, string baslik, string aciklama, string linkUrl, string kategori, string referansId, int? actionUserId = null)
        {
            try
            {
                if (string.IsNullOrEmpty(sicilNo)) return;

                // Kendine bildirim göndermeyi engelle (referans mantığı).
                if (actionUserId.HasValue)
                {
                    var actionUser = _context.tb_Kullanici.FirstOrDefault(u => u.KullaniciID == actionUserId.Value);
                    if (actionUser != null && actionUser.SicilNo == sicilNo) return;
                }

                var yeni = new tb_Notification
                {
                    SicilNo = sicilNo,
                    Baslik = baslik,
                    Aciklama = aciklama,
                    LinkUrl = linkUrl,
                    Kategori = kategori,
                    ReferansID = referansId,
                    Okundu = false,
                    KayitTarihi = DateTime.Now
                };
                _context.tb_Notification.Add(yeni);
                _context.SaveChanges();
            }
            catch
            {
                // referans: hata bildirim akışını bozmasın (sessiz).
            }
        }
    }
}
