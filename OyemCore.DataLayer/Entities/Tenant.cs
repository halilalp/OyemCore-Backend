using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OyemCore.DataLayer.Entities
{
    [Table("Tenant")]
    public class Tenant
    {
        [Key]
        public string TenantId { get; set; }
        public string Unvan { get; set; }
        public string ConnectionString { get; set; }
        public string? MailConnectionString { get; set; }
        public string? MeetingConnectionString { get; set; }
        public string? StorageFolder { get; set; }
        public bool IsActive { get; set; }
        public string? LdapServer { get; set; }
        public string? LdapDomain { get; set; }
        public string? ModulPaths { get; set; }
        public bool? IsMailService { get; set; }
        public bool? IsSmsService { get; set; }
        public string? ApiServer { get; set; }
    }
}
