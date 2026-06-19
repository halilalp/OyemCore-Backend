using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using WebPortalSpace.BusinessLayer.Common;
using WebPortalSpace.BusinessLayer.Interfaces;
using WebPortalSpace.DataLayer.Entities;
using WebPortalSpace.DataLayer.Interfaces;

namespace WebPortalSpace.BusinessLayer.Services
{
    public class AuthService : IAuthService
    {
        private readonly IYbsDbContext _context;
        private readonly ILdapService _ldapService;
        private readonly IConfiguration _configuration;

        public AuthService(IYbsDbContext context, ILdapService ldapService, IConfiguration configuration)
        {
            _context = context;
            _ldapService = ldapService;
            _configuration = configuration;
        }

        public (bool Success, string Token, string Message) Authenticate(string username, string password, string ipAddress, string userAgent)
        {
            try
            {
                string info = $" | IP: {ipAddress} | Cihaz: {userAgent}";
                string encryptedPassword = SecurityHelper.EncryptPassword(password);

                Console.WriteLine($"[AUTH SERVICE] Local Login check - Username: '{username}' | Hash: '{encryptedPassword}'");

                // 1. Manuel Kullanıcı Kontrolü
                var usrManual = _context.tb_Kullanici
                    .FirstOrDefault(u => u.KullaniciAdi == username && u.Sifre == encryptedPassword);

                if (usrManual != null)
                {
                    Console.WriteLine($"[AUTH SERVICE] Local Login successful for user: {usrManual.KullaniciID} ({usrManual.KullaniciAdi})");
                    if (usrManual.Durum == false)
                    {
                        LogKaydet(usrManual.Eposta, usrManual.SicilNo, "LOG_HATA", $"Hatalı Giriş: Kullanıcı Pasif (Manuel) - Kullanıcı: {username}{info}");
                        return (false, null, "Kullanıcı Pasif Durumda. Lütfen Bilgi İşlem departmanı ile iletişime geçiniz.");
                    }

                    string token = GenerateJwtToken(usrManual);

                    // Update SonGirisTar ve GirisSekli
                    usrManual.SonGirisTar = DateTime.Now;
                    usrManual.GirisSekli = Guid.NewGuid().ToString();
                    _context.SaveChanges();

                    LogKaydet(usrManual.Eposta, usrManual.SicilNo, "LOG", $"Sisteme Giriş Yapıldı. (Manuel){info}");
                    return (true, token, "Giriş Başarılı");
                }

                // 2. Active Directory (LDAP) Kontrolü
                var ldapResult = _ldapService.ValidateUser(username, password);
                if (ldapResult.Success && !string.IsNullOrEmpty(ldapResult.Email))
                {
                    string eposta = ldapResult.Email;
                    var per = _context.tb_Personel
                        .FirstOrDefault(p => p.Eposta.ToLower() == eposta.ToLower() && p.Durum == true);

                    if (per == null)
                    {
                        LogKaydet(eposta, "", "LOG_HATA", $"Hatalı Giriş: Netsis datası bulunamadı. Eposta: {eposta}{info}");
                        return (false, null, $"E-Postaya ait Netsis datası bulunamadı. Lütfen İK departmanı ile iletişime geçiniz. ({eposta})");
                    }

                    var usrAD = _context.tb_Kullanici
                        .FirstOrDefault(u => u.Eposta.ToLower() == eposta.ToLower() && u.SicilNo == per.SicilNo);

                    if (usrAD != null)
                    {
                        if (usrAD.Durum == false)
                        {
                            LogKaydet(usrAD.Eposta, usrAD.SicilNo, "LOG_HATA", $"Hatalı Giriş: Kullanıcı Pasif (AD) - Eposta: {eposta}{info}");
                            return (false, null, "Kullanıcı Pasif Durumda. Lütfen Bilgi İşlem departmanı ile iletişime geçiniz.");
                        }

                        // Update details
                        usrAD.SonGirisTar = DateTime.Now;
                        usrAD.GirisSekli = Guid.NewGuid().ToString();
                        usrAD.Unvan = per.Unvan;
                        usrAD.DepartmanKod = per.DepartmanKodu;
                        usrAD.Durum = true;
                        _context.SaveChanges();

                        string token = GenerateJwtToken(usrAD);
                        LogKaydet(usrAD.Eposta, usrAD.SicilNo, "LOG", $"Sisteme Giriş Yapıldı. (AD){info}");
                        return (true, token, "Giriş Başarılı");
                    }
                    else
                    {
                        // Yeni AD Kullanıcısı Oluşturma
                        var k = new tb_Kullanici
                        {
                            AdSoyad = per.AdSoyad,
                            DepartmanKod = per.DepartmanKodu,
                            Eposta = eposta,
                            Tel1 = per.Telefon,
                            SicilNo = per.SicilNo,
                            Unvan = per.Unvan,
                            Cinsiyet = string.IsNullOrEmpty(per.Cinsiyet) ? (char?)null : per.Cinsiyet[0],
                            SonGirisTar = DateTime.Now,
                            GirisSekli = Guid.NewGuid().ToString(),
                            Durum = true,
                            DefaultProje = 20, // HELPDESK
                            Yonetici = false,
                            ZimmetSorumlusu = false,
                            KayitTar = DateTime.Now,
                            YillikIzin = 0,
                            AdminBelgeTur = ""
                        };

                        _context.tb_Kullanici.Add(k);
                        _context.SaveChanges(); // k.KullaniciID EF tarafından doldurulur

                        // Varsayılan Yetkiler Ekleme (Set-based EF Core LINQ)
                        var sayfaIds = _context.tb_Sayfa
                            .AsNoTracking()
                            .Where(s => (s.ProjeID == 6 && (s.SayfaID == 24 || s.SayfaID == 25 || s.SayfaID == 26))
                                     || (s.ProjeID == 20 && (s.SayfaID == 1082 || s.SayfaID == 1083))
                                     || (s.ProjeID == 22 && (s.SayfaID == 1087 || s.SayfaID == 1088))
                                     || (s.ProjeID == 21)
                                     || (s.ProjeID == 23 && (s.SayfaID == 1091 || s.SayfaID == 1092))
                                     || (s.SayfaID == 30))
                            .Select(s => s.SayfaID)
                            .ToList();

                        foreach (var sayfaId in sayfaIds)
                        {
                            _context.tb_KullaniciYetki.Add(new tb_KullaniciYetki
                            {
                                KullaniciID = k.KullaniciID,
                                SayfaID = sayfaId,
                                KayitTar = DateTime.Now
                            });
                        }
                        _context.SaveChanges();

                        string token = GenerateJwtToken(k);
                        LogKaydet(eposta, "", "LOG", $"Yeni AD kullanıcısı girişi.{info}");
                        return (true, token, "Giriş Başarılı");
                    }
                }

                LogKaydet("", "", "LOG_HATA", $"Hatalı Giriş: Tanımsız Hata - Girilen: {username}{info}");
                return (false, null, "Kullanıcı Adı/Şifre bilgileri doğru değil.");
            }
            catch (Exception ex)
            {
                return (false, null, $"Kimlik doğrulama sırasında beklenmeyen hata: {ex.Message}");
            }
        }

        public (bool Success, string Message) ResetPassword(string sicilNo, string username)
        {
            try
            {
                var user = _context.tb_Kullanici
                    .FirstOrDefault(u => u.SicilNo == sicilNo);

                if (user == null)
                {
                    return (false, "Girilen Sicil Numarası ile eşleşen bir kayıt bulunamadı.");
                }

                bool isAdUser = string.IsNullOrEmpty(user.KullaniciAdi) && string.IsNullOrEmpty(user.Sifre);

                if (isAdUser)
                {
                    string emailPrefix = user.Eposta.Split('@')[0];
                    if (user.Eposta.Contains(username, StringComparison.OrdinalIgnoreCase) || emailPrefix.Equals(username, StringComparison.OrdinalIgnoreCase))
                    {
                        return (false, "Girmeye çalıştığınız bilgiler Active Directory (Domain) bilgileridir. Bilgisayarınızı açtığınız hesap bilgileri ile giriş yapabilirsiniz. Şifre süreniz dolmuş olabilir, lütfen kontrol ediniz.");
                    }
                    return (false, "Kullanıcı adı veya sicil numarası hatalı.");
                }
                else
                {
                    if (user.KullaniciAdi.Equals(username, StringComparison.OrdinalIgnoreCase))
                    {
                        // 8 haneli güçlü şifre oluştur
                        string upperRequest = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
                        string lowerRequest = "abcdefghijklmnopqrstuvwxyz";
                        string digitRequest = "0123456789";
                        string specialRequest = "!@#$%^&*?";

                        Random random = new Random();
                        char[] password = new char[8];

                        password[0] = upperRequest[random.Next(upperRequest.Length)];
                        password[1] = lowerRequest[random.Next(lowerRequest.Length)];
                        password[2] = digitRequest[random.Next(digitRequest.Length)];
                        password[3] = specialRequest[random.Next(specialRequest.Length)];

                        string allChars = upperRequest + lowerRequest + digitRequest + specialRequest;
                        for (int i = 4; i < 8; i++)
                        {
                            password[i] = allChars[random.Next(allChars.Length)];
                        }

                        password = password.OrderBy(x => random.Next()).ToArray();
                        string newPass = new string(password);

                        string encryptedPass = SecurityHelper.EncryptPassword(newPass);

                        user.Sifre = encryptedPass;
                        _context.SaveChanges();

                        if (string.IsNullOrEmpty(user.Tel1))
                        {
                            return (true, "Şifreniz sıfırlandı ancak sistemde kayıtlı cep telefonu numaranız bulunmadığı için SMS gönderilemedi. Lütfen yöneticinizle iletişime geçiniz. (Yeni Şifre Sistem Loglarına Kaydedildi)");
                        }

                        try
                        {
                            string alanTlfClean = user.Tel1.Replace(" ", "").Replace("(", "").Replace(")", "");
                            
                            var sms = new tb_Sms
                            {
                                Alan = user.AdSoyad,
                                AlanTlf = alanTlfClean,
                                Gonderen = "SMS-SISTEM",
                                Icerik = $"Sayın {user.AdSoyad}, WebPortalSpace yeni şifreniz: {newPass}",
                                KayitTarih = DateTime.Now,
                                Konu = "Sifre Sifirlama",
                                Durum = false,
                                TryCount = 0
                            };

                            _context.tb_Sms.Add(sms);
                            _context.SaveChanges();
                        }
                        catch (Exception exSms)
                        {
                            return (true, $"Şifre sıfırlandı fakat SMS gönderilirken hata oluştu: {exSms.Message}");
                        }

                        return (true, "Şifreniz başarıyla sıfırlandı ve kayıtlı cep telefonunuza SMS olarak gönderildi.");
                    }
                    return (false, "Kullanıcı adı veya sicil numarası hatalı.");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Sıfırlama işlemi sırasında hata oluştu: {ex.Message}");
            }
        }

        private string GenerateJwtToken(tb_Kullanici user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]);

            var personel = _context.tb_Personel
                .AsNoTracking()
                .FirstOrDefault(p => p.SicilNo == user.SicilNo);

            string sirketKodu = personel != null ? personel.SirketKodu : "0";

            string iseBasTarStr = "";
            if (personel != null && personel.IseBasTar.HasValue)
            {
                iseBasTarStr = personel.IseBasTar.Value.ToString("yyyy-MM-dd");
            }
            else if (user.KayitTar.HasValue)
            {
                iseBasTarStr = user.KayitTar.Value.ToString("yyyy-MM-dd");
            }

            string unvan = user.Unvan ?? (personel != null ? personel.Unvan : "") ?? "";
            string yillikIzin = (user.YillikIzin ?? 0).ToString();

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.KullaniciID.ToString()),
                new Claim(ClaimTypes.Name, user.AdSoyad),
                new Claim(ClaimTypes.Email, user.Eposta ?? ""),
                new Claim("SicilNo", user.SicilNo ?? ""),
                new Claim("AdminBelgeTur", user.AdminBelgeTur ?? ""),
                new Claim("SirketKodu", sirketKodu),
                new Claim("Unvan", unvan),
                new Claim("YillikIzin", yillikIzin),
                new Claim("IseBasTar", iseBasTarStr),
                new Claim("Yonetici", (user.Yonetici ?? false).ToString().ToLower()),
                new Claim("ZimmetSorumlusu", (user.ZimmetSorumlusu ?? false).ToString().ToLower()),
                new Claim("KullaniciAdi", user.KullaniciAdi ?? "")
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(double.Parse(_configuration["Jwt:ExpireMinutes"] ?? "1440")),
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"],
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        public void SavePushToken(int kullaniciID, string token)
        {
            try
            {
                // Clear this token from any other users to prevent multiple users receiving notifications on the same device
                var otherUsers = _context.tb_Kullanici
                    .Where(u => u.KullaniciID != kullaniciID && u.PushToken == token)
                    .ToList();

                foreach (var ou in otherUsers)
                {
                    ou.PushToken = null;
                }

                var user = _context.tb_Kullanici
                    .FirstOrDefault(u => u.KullaniciID == kullaniciID);

                if (user != null)
                {
                    user.PushToken = token;
                }
                
                _context.SaveChanges();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SavePushToken error: {ex}");
            }
        }

        public void ClearPushToken(int kullaniciID)
        {
            try
            {
                var user = _context.tb_Kullanici
                    .FirstOrDefault(u => u.KullaniciID == kullaniciID);

                if (user != null)
                {
                    user.PushToken = null;
                    _context.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ClearPushToken error: {ex}");
            }
        }

        private void LogKaydet(string eposta, string sicil, string konu, string aciklama)
        {
            try
            {
                if (aciklama != null && aciklama.Length > 150)
                {
                    aciklama = aciklama.Substring(0, 150);
                }

                var log = new tb_Log
                {
                    Eposta = eposta,
                    SicilNo = sicil,
                    Konu = konu,
                    Aciklama = aciklama,
                    KayitTar = DateTime.Now
                };

                _context.tb_Log.Add(log);
                _context.SaveChanges();
            }
            catch { }
        }
    }
}
