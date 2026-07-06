using System;
using System.Collections.Generic;
using OyemCore.DataLayer.Entities;

namespace OyemCore.BusinessLayer.Dtos
{
    public class BakimDropdownsDto
    {
        public bool IsAdmin { get; set; }
        public IEnumerable<tb_Sirket> Sirkets { get; set; }
        public IEnumerable<tb_Bolum> Bolums { get; set; }
        public IEnumerable<tb_Hat> Hats { get; set; }
        public IEnumerable<tb_Makine> Makines { get; set; }
    }
}
