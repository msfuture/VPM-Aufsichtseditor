using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VPMAufsichten
{
    public class AufsichtZeile
    {
        public string Kuerzel { get; set; }
        public string Nachname { get; set; }

        // Hilfswerte für die Logik (Zahlen aus dem VPM-Code)
        public int PauseID { get; set; }  // Die 2 aus "2-4-15"
        public int TagID { get; set; }    // Die 4 aus "2-4-15"
        public int OrtID { get; set; }    // Die 15 aus "2-4-15"

        // Optional: Die Anzeigenamen für das Debugging oder andere Ansichten
        public string OrtName { get; set; }
        public string[] OriginalZeile { get; set; } // Der komplette Satz aus der SPM
    }
}
