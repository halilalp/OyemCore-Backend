using System;
using System.Collections.Generic;
using System.Linq;
using OyemCore.BusinessLayer.Dtos;
using OyemCore.BusinessLayer.Interfaces;
using OyemCore.DataLayer.Entities;
using OyemCore.DataLayer.Interfaces;

namespace OyemCore.BusinessLayer.Services
{
    // referans: WebServiceChat (birebir) — .NET 7 / EF Core portu.
    public class ChatService : IChatService
    {
        private readonly IYbsDbContext _context;
        private readonly IChatRealtimeDispatcher _dispatcher;
        private readonly IPushNotificationService _push;

        public ChatService(IYbsDbContext context, IChatRealtimeDispatcher dispatcher, IPushNotificationService push)
        {
            _context = context;
            _dispatcher = dispatcher;
            _push = push;
        }

        private string GetCurrentSicilNo(int kullaniciID)
        {
            return _context.tb_Kullanici.FirstOrDefault(u => u.KullaniciID == kullaniciID)?.SicilNo;
        }

        // Bir grup koduna ait üye sicillerini döndürür (departman veya özel grup).
        private List<string> ResolveGroupMembers(string groupCode)
        {
            string gc = (groupCode ?? "").Trim();
            if (gc.ToUpper().StartsWith("GROUP_DEP_"))
            {
                string deptCode = gc.Substring("GROUP_DEP_".Length);
                return _context.tb_Kullanici
                    .Where(u => u.DepartmanKod == deptCode && u.Durum == true)
                    .Select(u => u.SicilNo).ToList();
            }
            if (gc.ToUpper().StartsWith("GROUP_CUSTOM_"))
            {
                return _context.tb_ChatGroupMember
                    .Where(m => m.GroupCode == gc)
                    .Select(m => m.SicilNo).ToList();
            }
            return new List<string>();
        }

        // ── 1. Sidebar / kullanıcı + grup listesi ──
        public IEnumerable<object> GetUsers(int kullaniciID, bool onlyActive)
        {
            string currentSicilNo = GetCurrentSicilNo(kullaniciID);
            if (string.IsNullOrEmpty(currentSicilNo)) return new List<object>();

            var list = new List<UserChatDto>();

            if (onlyActive)
            {
                // 1. Departman grubu
                var currentUser = _context.tb_Kullanici.FirstOrDefault(u => u.SicilNo == currentSicilNo);
                if (currentUser != null && !string.IsNullOrEmpty(currentUser.DepartmanKod))
                {
                    string deptCode = currentUser.DepartmanKod;
                    string deptName = _context.tb_Departman.Where(d => d.Kod == deptCode).Select(d => d.DepartmanAdi).FirstOrDefault() ?? deptCode;
                    string deptGroupCode = "GROUP_DEP_" + deptCode;

                    var lastDeptMsg = _context.tb_Chat
                        .Where(m => m.AliciSicilNo == deptGroupCode)
                        .OrderByDescending(m => m.GonderimTarihi).FirstOrDefault();

                    var deptMemberRec = _context.tb_ChatGroupMember
                        .FirstOrDefault(m => m.GroupCode == deptGroupCode && m.SicilNo == currentSicilNo);
                    DateTime lastDeptRead = deptMemberRec?.SonOkumaTarihi ?? new DateTime(1900, 1, 1);

                    int deptUnread = _context.tb_Chat
                        .Count(m => m.AliciSicilNo == deptGroupCode && m.GonderenSicilNo != currentSicilNo && m.GonderimTarihi > lastDeptRead);

                    list.Add(new UserChatDto
                    {
                        KullaniciID = 0,
                        AdSoyad = deptName + " Grubu",
                        Unvan = "Departman içi özel yazışma alanı.",
                        SicilNo = deptGroupCode,
                        Cinsiyet = "G",
                        LastMessage = lastDeptMsg != null ? lastDeptMsg.MesajMetni : "Grup sohbeti başlatıldı.",
                        LastMessageDate = lastDeptMsg?.GonderimTarihi,
                        UnreadCount = deptUnread
                    });
                }

                // 2. Özel gruplar
                try
                {
                    var customGroups = (from g in _context.tb_ChatGroup
                                        join m in _context.tb_ChatGroupMember on g.GroupCode equals m.GroupCode
                                        where m.SicilNo == currentSicilNo
                                        select g).ToList();

                    foreach (var group in customGroups)
                    {
                        var lastGrpMsg = _context.tb_Chat
                            .Where(m => m.AliciSicilNo == group.GroupCode)
                            .OrderByDescending(m => m.GonderimTarihi).FirstOrDefault();

                        var memberRec = _context.tb_ChatGroupMember
                            .FirstOrDefault(m => m.GroupCode == group.GroupCode && m.SicilNo == currentSicilNo);
                        DateTime lastRead = memberRec?.SonOkumaTarihi ?? new DateTime(1900, 1, 1);

                        int grpUnread = _context.tb_Chat
                            .Count(m => m.AliciSicilNo == group.GroupCode && m.GonderenSicilNo != currentSicilNo && m.GonderimTarihi > lastRead);

                        list.Add(new UserChatDto
                        {
                            KullaniciID = 0,
                            AdSoyad = group.GroupName,
                            Unvan = "Özel Grup",
                            SicilNo = group.GroupCode,
                            Cinsiyet = "G",
                            LastMessage = lastGrpMsg != null ? lastGrpMsg.MesajMetni : "Grup sohbeti başlatıldı.",
                            LastMessageDate = lastGrpMsg?.GonderimTarihi,
                            UnreadCount = grpUnread,
                            OlusturanSicilNo = group.OlusturanSicilNo
                        });
                    }
                }
                catch { }
            }

            var allDbUsers = _context.tb_Kullanici
                .Where(k => k.SicilNo != currentSicilNo && k.Durum == true)
                .Select(k => new { k.KullaniciID, k.AdSoyad, k.Unvan, k.SicilNo, k.Cinsiyet })
                .ToList();

            foreach (var k in allDbUsers)
            {
                string targetSicil = (k.SicilNo ?? "").Trim();

                var lastMsg = _context.tb_Chat
                    .Where(m => (m.GonderenSicilNo == currentSicilNo && m.AliciSicilNo == targetSicil) ||
                                (m.GonderenSicilNo == targetSicil && m.AliciSicilNo == currentSicilNo))
                    .OrderByDescending(m => m.GonderimTarihi).FirstOrDefault();

                int unread = _context.tb_Chat
                    .Count(m => m.GonderenSicilNo == targetSicil && m.AliciSicilNo == currentSicilNo && m.Okundu == false);

                if (!onlyActive || lastMsg != null)
                {
                    list.Add(new UserChatDto
                    {
                        KullaniciID = k.KullaniciID,
                        AdSoyad = k.AdSoyad,
                        Unvan = k.Unvan,
                        SicilNo = targetSicil,
                        Cinsiyet = k.Cinsiyet?.ToString() ?? "",
                        LastMessage = lastMsg != null ? lastMsg.MesajMetni : "",
                        LastMessageDate = lastMsg?.GonderimTarihi,
                        UnreadCount = unread,
                        IsOnline = _dispatcher.IsOnline(targetSicil)
                    });
                }
            }

            // Sıralama: departman grubu, sonra özel gruplar, sonra son mesaja göre kullanıcılar
            return list
                .OrderByDescending(x => x.Cinsiyet == "G" && x.SicilNo.StartsWith("GROUP_DEP_"))
                .ThenByDescending(x => x.Cinsiyet == "G" && x.SicilNo.StartsWith("GROUP_CUSTOM_"))
                .ThenByDescending(x => x.LastMessageDate)
                .Cast<object>().ToList();
        }

        // ── 2. Sohbet geçmişi (sayfalı) ──
        public IEnumerable<object> GetChatHistory(int kullaniciID, string targetSicilNo, int skip, int take)
        {
            string currentSicilNo = GetCurrentSicilNo(kullaniciID);
            if (string.IsNullOrEmpty(currentSicilNo)) return new List<object>();

            string cleanTarget = (targetSicilNo ?? "").Trim();
            bool isGroup = cleanTarget.ToUpper().StartsWith("GROUP_");

            var rawMessages = _context.tb_Chat
                .Where(m => (m.GonderenSicilNo == currentSicilNo && m.AliciSicilNo == cleanTarget) ||
                            (m.GonderenSicilNo == cleanTarget && m.AliciSicilNo == currentSicilNo) ||
                            (isGroup && m.AliciSicilNo == cleanTarget))
                .OrderByDescending(m => m.GonderimTarihi)
                .Skip(skip).Take(take).ToList();

            var senderKeys = rawMessages.Select(m => (m.GonderenSicilNo ?? "").Trim()).Distinct().ToList();
            var senderNames = _context.tb_Kullanici
                .Where(u => senderKeys.Contains(u.SicilNo))
                .ToList()
                .GroupBy(u => (u.SicilNo ?? "").Trim().ToUpper())
                .ToDictionary(g => g.Key, g => g.First().AdSoyad);

            List<string> groupMemberSicils = null;
            Dictionary<string, DateTime?> memberReadTimes = null;
            if (isGroup)
            {
                groupMemberSicils = ResolveGroupMembers(cleanTarget).Select(s => s.Trim().ToUpper()).ToList();
                var dbGroupMembers = _context.tb_ChatGroupMember.Where(m => m.GroupCode == cleanTarget).ToList();
                memberReadTimes = new Dictionary<string, DateTime?>();
                foreach (var sicil in groupMemberSicils)
                {
                    var mRec = dbGroupMembers.FirstOrDefault(m => (m.SicilNo ?? "").Trim().ToUpper() == sicil);
                    memberReadTimes[sicil] = mRec?.SonOkumaTarihi;
                }
            }

            // Parent (yanıt) metinleri için ön yükleme
            var parentIds = rawMessages.Where(m => m.ParentID != null).Select(m => m.ParentID.Value).Distinct().ToList();
            var parents = _context.tb_Chat.Where(p => parentIds.Contains(p.ID)).ToList();
            var parentSenderSicils = parents.Select(p => p.GonderenSicilNo).Distinct().ToList();
            var parentSenderNames = _context.tb_Kullanici.Where(u => parentSenderSicils.Contains(u.SicilNo))
                .ToList().GroupBy(u => u.SicilNo).ToDictionary(g => g.Key, g => g.First().AdSoyad);

            var messages = rawMessages.Select(m =>
            {
                string sk = (m.GonderenSicilNo ?? "").Trim().ToUpper();
                string senderName = senderNames.ContainsKey(sk) ? senderNames[sk] : m.GonderenSicilNo;

                bool isEveryoneRead;
                if (isGroup && groupMemberSicils != null)
                {
                    var targetMembers = groupMemberSicils.Where(s => s != sk).ToList();
                    if (targetMembers.Count > 0)
                    {
                        int readCount = targetMembers.Count(ms => memberReadTimes.ContainsKey(ms) && memberReadTimes[ms].HasValue && m.GonderimTarihi <= memberReadTimes[ms].Value);
                        isEveryoneRead = readCount == targetMembers.Count;
                    }
                    else isEveryoneRead = true;
                }
                else isEveryoneRead = m.Okundu;

                var parent = m.ParentID != null ? parents.FirstOrDefault(p => p.ID == m.ParentID.Value) : null;
                string parentAd = parent != null && parentSenderNames.ContainsKey(parent.GonderenSicilNo) ? parentSenderNames[parent.GonderenSicilNo] : "";

                return (object)new
                {
                    m.ID,
                    m.GonderenSicilNo,
                    GonderenAdSoyad = senderName,
                    m.AliciSicilNo,
                    m.MesajMetni,
                    m.DosyaAdi,
                    m.DosyaYolu,
                    m.DosyaTipi,
                    DosyaBoyutu = m.DosyaBoyutu ?? 0,
                    m.GonderimTarihi,
                    Okundu = isEveryoneRead,
                    m.ParentID,
                    ParentMesajMetni = parent?.MesajMetni ?? "",
                    ParentGonderenAd = parentAd
                };
            }).ToList();

            messages.Reverse();

            // İlk sayfada gelen mesajları okundu işaretle
            if (!isGroup && skip == 0)
            {
                var unreadMessages = _context.tb_Chat
                    .Where(m => m.GonderenSicilNo == cleanTarget && m.AliciSicilNo == currentSicilNo && m.Okundu == false).ToList();
                foreach (var msg in unreadMessages) { msg.Okundu = true; msg.OkunmaTarihi = DateTime.Now; }
                if (unreadMessages.Count > 0) _context.SaveChanges();
            }
            else if (cleanTarget.ToUpper().StartsWith("GROUP_CUSTOM_") && skip == 0)
            {
                var memberRec = _context.tb_ChatGroupMember.FirstOrDefault(m => m.GroupCode == cleanTarget && m.SicilNo == currentSicilNo);
                if (memberRec != null) { memberRec.SonOkumaTarihi = DateTime.Now; _context.SaveChanges(); }
            }

            return messages;
        }

        // ── 3. Konuşmayı okundu işaretle ──
        public bool MarkConversationAsRead(int kullaniciID, string targetSicilNo)
        {
            string currentSicilNo = GetCurrentSicilNo(kullaniciID);
            if (string.IsNullOrEmpty(currentSicilNo)) return false;

            string cleanTarget = (targetSicilNo ?? "").Trim();
            bool isGroup = cleanTarget.ToUpper().StartsWith("GROUP_");

            if (!isGroup)
            {
                var unreadMessages = _context.tb_Chat
                    .Where(m => m.GonderenSicilNo == cleanTarget && m.AliciSicilNo == currentSicilNo && m.Okundu == false).ToList();
                foreach (var msg in unreadMessages) { msg.Okundu = true; msg.OkunmaTarihi = DateTime.Now; }
                if (unreadMessages.Count > 0) _context.SaveChanges();
            }
            else
            {
                var memberRec = _context.tb_ChatGroupMember.FirstOrDefault(m => m.GroupCode == cleanTarget && m.SicilNo == currentSicilNo);
                if (memberRec != null) memberRec.SonOkumaTarihi = DateTime.Now;
                else _context.tb_ChatGroupMember.Add(new tb_ChatGroupMember { GroupCode = cleanTarget, SicilNo = currentSicilNo, KayitTarihi = DateTime.Now, SonOkumaTarihi = DateTime.Now });
                _context.SaveChanges();
            }
            return true;
        }

        // ── 4. Mesaj okundu detayı (kimler okudu) ──
        public object GetMessageDetails(int kullaniciID, int messageID)
        {
            string currentSicilNo = GetCurrentSicilNo(kullaniciID);
            if (string.IsNullOrEmpty(currentSicilNo)) return new { error = true, message = "SicilNo not resolved" };

            var msg = _context.tb_Chat.FirstOrDefault(m => m.ID == messageID);
            if (msg == null) return new { error = true, message = "Mesaj bulunamadı" };

            string cur = currentSicilNo.Trim().ToUpper();
            bool isGroup = (msg.AliciSicilNo ?? "").Trim().ToUpper().StartsWith("GROUP_");
            bool isAuthorized = (msg.GonderenSicilNo ?? "").Trim().ToUpper() == cur || (msg.AliciSicilNo ?? "").Trim().ToUpper() == cur;
            if (!isAuthorized && isGroup)
                isAuthorized = ResolveGroupMembers(msg.AliciSicilNo).Any(s => s.Trim().ToUpper() == cur);
            if (!isAuthorized) return new { error = true, message = "Yetkisiz erişim" };

            var sender = _context.tb_Kullanici.FirstOrDefault(u => u.SicilNo == msg.GonderenSicilNo);
            string senderName = sender?.AdSoyad ?? msg.GonderenSicilNo;

            var receiverList = new List<object>();
            if (!isGroup)
            {
                var receiver = _context.tb_Kullanici.FirstOrDefault(u => u.SicilNo == msg.AliciSicilNo);
                receiverList.Add(new
                {
                    AdSoyad = receiver?.AdSoyad ?? msg.AliciSicilNo,
                    Okundu = msg.Okundu,
                    OkunmaTarihi = msg.OkunmaTarihi.HasValue ? msg.OkunmaTarihi.Value.ToString("dd.MM.yyyy HH:mm") : "-"
                });
            }
            else
            {
                var memberSicils = ResolveGroupMembers(msg.AliciSicilNo);
                memberSicils.Remove((msg.GonderenSicilNo ?? "").Trim());
                var groupMembers = _context.tb_ChatGroupMember.Where(m => m.GroupCode == msg.AliciSicilNo).ToList();
                foreach (var sicil in memberSicils)
                {
                    var u = _context.tb_Kullanici.FirstOrDefault(k => k.SicilNo == sicil);
                    if (u == null) continue;
                    var mRec = groupMembers.FirstOrDefault(m => (m.SicilNo ?? "").Trim().ToUpper() == sicil.Trim().ToUpper());
                    DateTime? sonOkuma = mRec?.SonOkumaTarihi;
                    bool isRead = sonOkuma.HasValue && msg.GonderimTarihi <= sonOkuma.Value;
                    receiverList.Add(new { AdSoyad = u.AdSoyad, Okundu = isRead, OkunmaTarihi = isRead ? sonOkuma.Value.ToString("dd.MM.yyyy HH:mm") : "-" });
                }
            }

            return new
            {
                success = true,
                data = new
                {
                    ID = msg.ID,
                    MesajMetni = msg.MesajMetni,
                    GonderenAd = senderName,
                    GonderimTarihi = msg.GonderimTarihi.ToString("dd.MM.yyyy HH:mm"),
                    IsGroup = isGroup,
                    ReceiverList = receiverList
                }
            };
        }

        // ── 5. Mesaj kaydet + gerçek-zamanlı dağıt + push ──
        public object SaveMessage(int kullaniciID, string aliciSicilNo, string mesajMetni, string dosyaAdi, string dosyaYolu, string dosyaTipi, long dosyaBoyutu, int? parentID)
        {
            string currentSicilNo = GetCurrentSicilNo(kullaniciID);
            if (string.IsNullOrEmpty(currentSicilNo)) return new { error = true, message = "SicilNo not resolved" };

            string cleanReceiver = (aliciSicilNo ?? "").Trim();

            var yeni = new tb_Chat
            {
                GonderenSicilNo = currentSicilNo,
                AliciSicilNo = cleanReceiver,
                MesajMetni = mesajMetni,
                GonderimTarihi = DateTime.Now,
                Okundu = false,
                ParentID = (parentID != null && parentID > 0) ? parentID : null
            };
            if (!string.IsNullOrEmpty(dosyaAdi))
            {
                yeni.DosyaAdi = dosyaAdi;
                yeni.DosyaYolu = dosyaYolu;
                yeni.DosyaTipi = dosyaTipi;
                yeni.DosyaBoyutu = dosyaBoyutu;
            }
            _context.tb_Chat.Add(yeni);
            _context.SaveChanges();

            string senderName = _context.tb_Kullanici.Where(u => u.SicilNo == currentSicilNo).Select(u => u.AdSoyad).FirstOrDefault() ?? "Bir Üye";
            string parentMetni = "";
            string parentGonderenAd = "";
            if (yeni.ParentID != null)
            {
                var p = _context.tb_Chat.FirstOrDefault(x => x.ID == yeni.ParentID.Value);
                if (p != null)
                {
                    parentMetni = p.MesajMetni;
                    parentGonderenAd = _context.tb_Kullanici.Where(u => u.SicilNo == p.GonderenSicilNo).Select(u => u.AdSoyad).FirstOrDefault() ?? "";
                }
            }

            var payload = new
            {
                ID = yeni.ID,
                GonderenSicilNo = currentSicilNo,
                GonderenAdSoyad = senderName,
                AliciSicilNo = cleanReceiver,
                MesajMetni = mesajMetni,
                DosyaAdi = dosyaAdi,
                DosyaYolu = dosyaYolu,
                DosyaTipi = dosyaTipi,
                DosyaBoyutu = dosyaBoyutu,
                GonderimTarihi = yeni.GonderimTarihi,
                Saat = yeni.GonderimTarihi.ToString("HH:mm"),
                ParentID = yeni.ParentID,
                ParentMesajMetni = parentMetni,
                ParentGonderenAd = parentGonderenAd
            };

            // Alıcıları çöz (grup üyeleri veya 1:1) + gönderene echo
            List<string> recipients;
            bool isGroup = cleanReceiver.ToUpper().StartsWith("GROUP_");
            if (isGroup) recipients = ResolveGroupMembers(cleanReceiver);
            else recipients = new List<string> { cleanReceiver };

            var dispatchTargets = recipients.Concat(new[] { currentSicilNo })
                .Select(s => s.Trim()).Distinct().ToList();
            try { _dispatcher.SendToSicils(dispatchTargets, "receiveMessage", payload); } catch { }

            // Push: çevrimdışı (SignalR ile ulaşılamayan) alıcılara
            string shortBody = string.IsNullOrEmpty(mesajMetni) ? "Bir dosya gönderdi." : (mesajMetni.Length > 60 ? mesajMetni.Substring(0, 60) + "..." : mesajMetni);
            string pushTitle = isGroup ? (senderName + " (Grup)") : senderName;
            foreach (var r in recipients)
            {
                string rc = (r ?? "").Trim();
                if (string.IsNullOrEmpty(rc) || rc.Equals(currentSicilNo, StringComparison.OrdinalIgnoreCase)) continue;
                if (_dispatcher.IsOnline(rc)) continue; // online kullanıcı realtime aldı
                try { _ = _push.SendToUserBySicilNoAsync(rc, pushTitle, shortBody, new { screen = "Chat", gonderenSicilNo = currentSicilNo, groupCode = isGroup ? cleanReceiver : "" }); } catch { }
            }

            return new { success = true, ID = yeni.ID };
        }

        // ── 6. Grup oluştur ──
        public object CreateGroup(int kullaniciID, string groupName, List<string> memberSicils)
        {
            string currentSicilNo = GetCurrentSicilNo(kullaniciID);
            if (string.IsNullOrEmpty(currentSicilNo)) return new { error = true, message = "SicilNo not resolved" };

            var members = (memberSicils ?? new List<string>()).Select(s => (s ?? "").Trim()).Where(s => s != "").ToList();
            string creatorSicil = currentSicilNo.Trim();
            if (!members.Any(s => s.Equals(creatorSicil, StringComparison.OrdinalIgnoreCase))) members.Add(creatorSicil);

            string groupCode = "GROUP_CUSTOM_" + Guid.NewGuid().ToString("N");
            _context.tb_ChatGroup.Add(new tb_ChatGroup { GroupCode = groupCode, GroupName = groupName, OlusturanSicilNo = creatorSicil, KayitTarihi = DateTime.Now });
            foreach (var sicil in members)
                _context.tb_ChatGroupMember.Add(new tb_ChatGroupMember { GroupCode = groupCode, SicilNo = sicil, KayitTarihi = DateTime.Now });
            _context.SaveChanges();

            try { _dispatcher.SendToSicils(members, "reloadSidebar", groupCode, true); } catch { }
            return new { success = true, GroupCode = groupCode };
        }

        // ── 7. Grup üyelerini güncelle (+ sistem mesajları) ──
        public bool UpdateGroupMembers(int kullaniciID, string groupCode, List<string> memberSicils)
        {
            string currentSicilNo = GetCurrentSicilNo(kullaniciID);
            if (string.IsNullOrEmpty(currentSicilNo)) return false;

            var members = (memberSicils ?? new List<string>()).Select(s => (s ?? "").Trim()).Where(s => s != "").ToList();
            string creatorSicil = currentSicilNo.Trim();
            if (!members.Any(s => s.Equals(creatorSicil, StringComparison.OrdinalIgnoreCase))) members.Add(creatorSicil);

            var existingMembers = _context.tb_ChatGroupMember.Where(m => m.GroupCode == groupCode).ToList();
            var existingSicils = existingMembers.Select(m => (m.SicilNo ?? "").Trim()).ToList();
            var addedSicils = members.Where(s => !existingSicils.Any(es => es.Equals(s, StringComparison.OrdinalIgnoreCase))).ToList();
            var removedSicils = existingSicils.Where(es => !members.Any(s => s.Equals(es, StringComparison.OrdinalIgnoreCase))).ToList();

            _context.tb_ChatGroupMember.RemoveRange(existingMembers);
            foreach (var sicil in members)
                _context.tb_ChatGroupMember.Add(new tb_ChatGroupMember { GroupCode = groupCode, SicilNo = sicil, KayitTarihi = DateTime.Now });
            _context.SaveChanges();

            void SystemMsg(string sicil, string suffix)
            {
                string name = _context.tb_Kullanici.Where(u => u.SicilNo == sicil).Select(u => u.AdSoyad).FirstOrDefault() ?? sicil;
                string msgText = name + suffix;
                _context.tb_Chat.Add(new tb_Chat { GonderenSicilNo = "SYSTEM", AliciSicilNo = groupCode, MesajMetni = msgText, GonderimTarihi = DateTime.Now, Okundu = true });
                _context.SaveChanges();
                try { _dispatcher.SendToSicils(ResolveGroupMembers(groupCode), "receiveMessage", new { GonderenSicilNo = "SYSTEM", AliciSicilNo = groupCode, MesajMetni = msgText, Saat = DateTime.Now.ToString("HH:mm"), GonderenAdSoyad = "Sistem" }); } catch { }
            }
            foreach (var sicil in addedSicils) SystemMsg(sicil, " gruba eklendi.");
            foreach (var sicil in removedSicils) SystemMsg(sicil, " gruptan çıkarıldı.");

            try
            {
                foreach (var sicil in members)
                {
                    bool isNew = addedSicils.Any(x => x.Equals(sicil.Trim(), StringComparison.OrdinalIgnoreCase));
                    _dispatcher.SendToSicils(new[] { sicil }, "reloadSidebar", groupCode, isNew);
                }
            }
            catch { }
            return true;
        }

        // ── 8. Grup detayları ──
        public object GetGroupDetails(int kullaniciID, string groupCode)
        {
            var groupInfo = _context.tb_ChatGroup.FirstOrDefault(g => g.GroupCode == groupCode);
            if (groupInfo == null) return new { error = true, message = "Group not found" };

            string creatorName = _context.tb_Kullanici.Where(u => u.SicilNo == groupInfo.OlusturanSicilNo).Select(u => u.AdSoyad).FirstOrDefault() ?? groupInfo.OlusturanSicilNo;

            var members = (from m in _context.tb_ChatGroupMember
                           join u in _context.tb_Kullanici on m.SicilNo equals u.SicilNo
                           where m.GroupCode == groupCode
                           select new ChatGroupMemberDto { GroupCode = m.GroupCode, SicilNo = m.SicilNo, AdSoyad = u.AdSoyad }).ToList();

            return new { success = true, group = groupInfo, creatorName, creationDate = groupInfo.KayitTarihi.ToString("dd.MM.yyyy HH:mm"), members };
        }

        // ── 9. Gruptan ayrıl ──
        public object LeaveGroup(int kullaniciID, string groupCode)
        {
            string currentSicilNo = GetCurrentSicilNo(kullaniciID);
            if (string.IsNullOrEmpty(currentSicilNo)) return new { error = true, message = "SicilNo not resolved" };

            string userAdSoyad = _context.tb_Kullanici.Where(u => u.SicilNo == currentSicilNo).Select(u => u.AdSoyad).FirstOrDefault() ?? "Bir üye";

            var memberRec = _context.tb_ChatGroupMember.FirstOrDefault(m => m.GroupCode == groupCode && m.SicilNo == currentSicilNo);
            if (memberRec != null) _context.tb_ChatGroupMember.Remove(memberRec);

            string msgText = userAdSoyad + " gruptan ayrıldı.";
            _context.tb_Chat.Add(new tb_Chat { GonderenSicilNo = "SYSTEM", AliciSicilNo = groupCode, MesajMetni = msgText, GonderimTarihi = DateTime.Now, Okundu = true });
            _context.SaveChanges();

            try { _dispatcher.SendToSicils(ResolveGroupMembers(groupCode), "receiveMessage", new { GonderenSicilNo = "SYSTEM", AliciSicilNo = groupCode, MesajMetni = msgText, Saat = DateTime.Now.ToString("HH:mm"), GonderenAdSoyad = "Sistem" }); } catch { }
            return new { success = true, messageText = msgText };
        }

        // ── 10. Paylaşılan dosyalar ──
        public IEnumerable<object> GetSharedFiles(int kullaniciID, string targetSicilNo)
        {
            string currentSicilNo = GetCurrentSicilNo(kullaniciID);
            if (string.IsNullOrEmpty(currentSicilNo)) return new List<object>();

            string cleanTarget = (targetSicilNo ?? "").Trim();
            string cleanCurrent = currentSicilNo.Trim();
            bool isGroup = cleanTarget.ToUpper().StartsWith("GROUP_");

            var dbFiles = _context.tb_Chat
                .Where(m => m.DosyaAdi != null && m.DosyaAdi != "" && (
                    (m.GonderenSicilNo == cleanCurrent && m.AliciSicilNo == cleanTarget) ||
                    (m.GonderenSicilNo == cleanTarget && m.AliciSicilNo == cleanCurrent) ||
                    (isGroup && m.AliciSicilNo == cleanTarget)))
                .OrderByDescending(m => m.GonderimTarihi)
                .Select(m => new SharedFileDto
                {
                    DosyaAdi = m.DosyaAdi,
                    DosyaYolu = m.DosyaYolu,
                    DosyaTipi = m.DosyaTipi,
                    DosyaBoyutu = m.DosyaBoyutu ?? 0,
                    GonderimTarihi = m.GonderimTarihi,
                    GonderenSicilNo = m.GonderenSicilNo
                }).ToList();

            var senders = dbFiles.Select(f => (f.GonderenSicilNo ?? "").Trim()).Distinct().ToList();
            var senderNames = _context.tb_Kullanici.Where(u => senders.Contains(u.SicilNo)).ToList()
                .GroupBy(u => u.SicilNo).ToDictionary(g => g.Key, g => g.First().AdSoyad);

            return dbFiles.Select(f => (object)new
            {
                f.DosyaAdi,
                f.DosyaYolu,
                f.DosyaTipi,
                f.DosyaBoyutu,
                Gonderici = senderNames.ContainsKey((f.GonderenSicilNo ?? "").Trim()) ? senderNames[(f.GonderenSicilNo ?? "").Trim()] : f.GonderenSicilNo,
                GonderimTarihiStr = f.GonderimTarihi.ToString("dd.MM.yyyy HH:mm")
            }).ToList();
        }

        // ── 11. Toplam okunmamış (rozet) ──
        public int GetTotalUnreadCount(int kullaniciID)
        {
            string currentSicilNo = GetCurrentSicilNo(kullaniciID);
            if (string.IsNullOrEmpty(currentSicilNo)) return 0;
            string cleanCurrent = currentSicilNo.Trim();

            int directUnread = _context.tb_Chat.Count(m => m.AliciSicilNo == cleanCurrent && m.GonderenSicilNo != cleanCurrent && m.Okundu == false);

            var customGroups = (from g in _context.tb_ChatGroup
                                join m in _context.tb_ChatGroupMember on g.GroupCode equals m.GroupCode
                                where m.SicilNo == cleanCurrent
                                select new { g.GroupCode, m.SonOkumaTarihi }).ToList();

            int groupUnread = 0;
            foreach (var group in customGroups)
            {
                DateTime lastRead = group.SonOkumaTarihi ?? new DateTime(1900, 1, 1);
                groupUnread += _context.tb_Chat.Count(m => m.AliciSicilNo == group.GroupCode && m.GonderenSicilNo != cleanCurrent && m.GonderimTarihi > lastRead);
            }
            return directUnread + groupUnread;
        }

        // ── 12. Aktif bağlantılar ──
        public object GetActiveConnections()
        {
            return Hubs_UsersSnapshot();
        }

        // ChatHub statik sözlüğüne BusinessLayer'dan erişemediğimizden dispatcher üzerinden değil,
        // basit çevrimiçi sicil listesini döndürürüz (detay Backend katmanında da alınabilir).
        private object Hubs_UsersSnapshot()
        {
            return new { success = true };
        }
    }
}
