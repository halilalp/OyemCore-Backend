using System;
using System.ComponentModel.DataAnnotations;

namespace OyemCore.DataLayer.Entities
{
    public class tb_SayimAygit
    {
        [Key]
        public int AygitID { get; set; }
        public string SicilNo { get; set; }
        public DateTime? IslemTar { get; set; }
    }
}
