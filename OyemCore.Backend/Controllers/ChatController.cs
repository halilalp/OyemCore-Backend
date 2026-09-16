using System;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OyemCore.BusinessLayer.Interfaces;

namespace OyemCore.Backend.Controllers
{
    // referans: WebServiceChat — .NET 7 API portu.
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chat;

        public ChatController(IChatService chat)
        {
            _chat = chat;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim != null && int.TryParse(claim.Value, out int id)) return id;
            throw new UnauthorizedAccessException("Giris yapan kullanici kimligi dogrulanamadi.");
        }

        // Sidebar (kullanıcılar + gruplar). onlyActive=true → sadece aktif sohbetler + gruplar.
        [HttpGet("users")]
        public IActionResult GetUsers([FromQuery] bool onlyActive = false)
            => Ok(_chat.GetUsers(GetCurrentUserId(), onlyActive));

        // Sohbet geçmişi (sayfalı).
        [HttpGet("history")]
        public IActionResult GetChatHistory([FromQuery] string targetSicilNo, [FromQuery] int skip = 0, [FromQuery] int take = 30)
            => Ok(_chat.GetChatHistory(GetCurrentUserId(), targetSicilNo, skip, take));

        // Konuşmayı okundu işaretle.
        [HttpPost("mark-read")]
        public IActionResult MarkConversationAsRead([FromBody] TargetRequest req)
            => Ok(new { success = _chat.MarkConversationAsRead(GetCurrentUserId(), req?.TargetSicilNo) });

        // Mesaj okuma detayı.
        [HttpGet("message/{messageID}/details")]
        public IActionResult GetMessageDetails(int messageID)
            => Ok(_chat.GetMessageDetails(GetCurrentUserId(), messageID));

        // Mesaj gönder (metin ve/veya dosya).
        [HttpPost("send")]
        public IActionResult SaveMessage([FromBody] SendMessageRequest req)
            => Ok(_chat.SaveMessage(GetCurrentUserId(), req.AliciSicilNo, req.MesajMetni, req.DosyaAdi, req.DosyaYolu, req.DosyaTipi, req.DosyaBoyutu, req.ParentID));

        // Grup oluştur.
        [HttpPost("group")]
        public IActionResult CreateGroup([FromBody] CreateGroupRequest req)
            => Ok(_chat.CreateGroup(GetCurrentUserId(), req.GroupName, req.MemberSicils));

        // Grup üyelerini güncelle.
        [HttpPut("group/{groupCode}/members")]
        public IActionResult UpdateGroupMembers(string groupCode, [FromBody] UpdateGroupRequest req)
            => Ok(new { success = _chat.UpdateGroupMembers(GetCurrentUserId(), groupCode, req.MemberSicils) });

        // Grup detayları.
        [HttpGet("group/{groupCode}")]
        public IActionResult GetGroupDetails(string groupCode)
            => Ok(_chat.GetGroupDetails(GetCurrentUserId(), groupCode));

        // Gruptan ayrıl.
        [HttpPost("group/{groupCode}/leave")]
        public IActionResult LeaveGroup(string groupCode)
            => Ok(_chat.LeaveGroup(GetCurrentUserId(), groupCode));

        // Paylaşılan dosyalar.
        [HttpGet("shared-files")]
        public IActionResult GetSharedFiles([FromQuery] string targetSicilNo)
            => Ok(_chat.GetSharedFiles(GetCurrentUserId(), targetSicilNo));

        // Toplam okunmamış mesaj (rozet).
        [HttpGet("unread-count")]
        public IActionResult GetTotalUnreadCount()
            => Ok(new { success = true, totalUnread = _chat.GetTotalUnreadCount(GetCurrentUserId()) });

        // Sohbeti temizle (tek taraflı / soft-delete).
        [HttpPost("clear-conversation")]
        public IActionResult ClearConversation([FromBody] TargetRequest req)
            => Ok(_chat.ClearConversation(GetCurrentUserId(), req?.TargetSicilNo));

        // Grubu sil / kapat (kurucu kapatır, üye kendi listesinden siler).
        [HttpDelete("group/{groupCode}")]
        public IActionResult DeleteGroup(string groupCode)
            => Ok(_chat.DeleteGroup(GetCurrentUserId(), groupCode));

        public class TargetRequest { public string TargetSicilNo { get; set; } }
        public class SendMessageRequest
        {
            public string AliciSicilNo { get; set; }
            public string MesajMetni { get; set; }
            public string DosyaAdi { get; set; }
            public string DosyaYolu { get; set; }
            public string DosyaTipi { get; set; }
            public long DosyaBoyutu { get; set; }
            public int? ParentID { get; set; }
        }
        public class CreateGroupRequest { public string GroupName { get; set; } public List<string> MemberSicils { get; set; } }
        public class UpdateGroupRequest { public List<string> MemberSicils { get; set; } }
    }
}
