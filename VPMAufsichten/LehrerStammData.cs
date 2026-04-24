using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VPMAufsichten
{
    public class LehrerStammData
    {
        public string Kuerzel { get; set; }
        public string Nachname { get; set; }
        public string[] OriginalZeilenArray { get; set; } // Alle Spalten (0 bis X)
    }
}
