using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VPMAufsichten
{
    public class PlanZeile
    {
        public string Ort { get; set; }
        public int OrtIndex { get; set; } // Wichtig für den Rückexport (die "15" aus 2-4-15)

        // Die Spalten für die Wochentage
        public string Montag { get; set; }
        public string Dienstag { get; set; }
        public string Mittwoch { get; set; }
        public string Donnerstag { get; set; }
        public string Freitag { get; set; }
    }
}
