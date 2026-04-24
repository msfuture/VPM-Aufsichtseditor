using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using Path = System.IO.Path;
using System.ComponentModel;

namespace VPMAufsichten
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 

    
    public partial class MainWindow : Window
    {
        string[] aOrte;
        List<AufsichtZeile> aLehrer;
        private ICollectionView _planView;
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFolderDialog dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Ordner mit VPM-Daten auswählen"
            };
            if(dialog.ShowDialog()== true)
            {
                string selectedPath = dialog.FolderName;
                LoadVpmData(selectedPath);
                aLehrer = ParseSpmFile(selectedPath + "\\vpm_lehrer.spm", aOrte);

            }
        }

        private void LoadVpmData(string pfad)
        {
            aOrte = GetAufsichtsorte(pfad);
        }

        public List<AufsichtZeile> ParseSpmFile(string filePath, string[] aufsichtsOrte)
        {
            var liste = new List<AufsichtZeile>();
            // Wir nutzen Windows-1252, falls Umlaute in Namen/Kürzeln Probleme machen
            var lines = File.ReadAllLines(filePath, Encoding.Latin1);

            foreach (var line in lines)
            {
                var parts = line.Split('\t');
                if (parts.Length >= 6)
                {
                    string kuerzel = parts[0];
                    string nachname = parts[1];
                    string spaltenInhalt = parts[5]; // Die Spalte mit "2-4-15,1-2-3"

                    if (string.IsNullOrWhiteSpace(spaltenInhalt)) continue;

                    // 1. Am Komma splitten für mehrere Aufsichten
                    var alleCodes = spaltenInhalt.Split(',', StringSplitOptions.RemoveEmptyEntries);

                    foreach (var code in alleCodes)
                    {
                        // 2. Den einzelnen Code (z.B. 2-4-15) splitten
                        var codeParts = code.Trim().Split('-');
                        if (codeParts.Length == 3)
                        {
                            if (int.TryParse(codeParts[0], out int pauseNr) &&
                                int.TryParse(codeParts[1], out int tagNr) &&
                                int.TryParse(codeParts[2], out int ortIndexRaw))
                            {
                                int ortIndex = ortIndexRaw - 1; // VPM ist 1-basiert

                                string ortName = (ortIndex >= 0 && ortIndex < aufsichtsOrte.Length)
                                                 ? aufsichtsOrte[ortIndex]
                                                 : $"Ort {ortIndexRaw}";

                                var neueAufsicht = new AufsichtZeile
                                {
                                    Kuerzel = kuerzel,
                                    Nachname = nachname,
                                    PauseID = int.Parse(codeParts[0]),
                                    TagID = int.Parse(codeParts[1]),
                                    OrtID = int.Parse(codeParts[2]),
                                    OriginalZeile = parts // Die komplette ursprüngliche Zeile sichern
                                };
                                liste.Add(neueAufsicht);
                            }
                        }
                    }
                }
            }
            return liste;
        }

        private string GetWochentagName(int tagNr)
        {
            return tagNr switch
            {
                1 => "Montag",
                2 => "Dienstag",
                3 => "Mittwoch",
                4 => "Donnerstag",
                5 => "Freitag",
                _ => "Unbekannt"
            };
        }

        private string[] GetAufsichtsorte(string folderPath)
        {
            string iniPath = Path.Combine(folderPath, "vpm.ini");

            if (!File.Exists(iniPath))
            {
                MessageBox.Show("Die Datei vpm.ini wurde im gewählten Ordner nicht gefunden.");
                return Array.Empty<string>();
            }

            try
            {
                // Wir suchen die Zeile, die mit "Pausenaufsichtsorte=" beginnt.
                // Wichtig: Encoding.Default oder CodePages 1252 nutzen, falls Umlaute falsch dargestellt werden.
                var targetLine = File.ReadLines(iniPath, Encoding.Latin1)
                                     .FirstOrDefault(line => line.StartsWith("Pausenaufsichtsorte=", StringComparison.OrdinalIgnoreCase));

                if (targetLine != null)
                {
                    // Den Präfix "Pausenaufsichtsorte=" abschneiden
                    string werte = targetLine.Substring("Pausenaufsichtsorte=".Length);

                    // Am Pipe-Symbol | trennen und leere Einträge entfernen
                    string[] orte = werte.Split('|', StringSplitOptions.RemoveEmptyEntries);

                    return orte;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Lesen der INI: {ex.Message}");
            }

            return Array.Empty<string>();
        }

        private void RefreshGrid(int pause)
        {
            var planDaten = new List<PlanZeile>();

            // Wir gehen alle Orte aus deinem INI-Array durch
            for (int i = 0; i < aOrte.Length; i++)
            {
                var zeile = new PlanZeile
                {
                    Ort = aOrte[i],
                    OrtIndex = i + 1 // VPM Index ist 1-basiert
                };

                // Jetzt suchen wir in den eingelesenen SPM-Daten nach Lehrern, 
                // die in DIESER Pause an DIESEM Ort eingeteilt sind.
                zeile.Montag = GetLehrerFuer(pause, 1, i + 1);
                zeile.Dienstag = GetLehrerFuer(pause, 2, i + 1);
                zeile.Mittwoch = GetLehrerFuer(pause, 3, i + 1);
                zeile.Donnerstag = GetLehrerFuer(pause, 4, i + 1);
                zeile.Freitag = GetLehrerFuer(pause, 5, i + 1);

                planDaten.Add(zeile);
            }

            _planView = CollectionViewSource.GetDefaultView(planDaten);

            // Die Filter-Regel definieren
            _planView.Filter = item =>
            {
                if (uiHideEmpty.IsChecked == false) return true;

                var zeile = item as PlanZeile;
                if (zeile == null) return false;

                // Die Zeile anzeigen, wenn in irgendeinem Tag ein Kürzel steht
                return !string.IsNullOrWhiteSpace(zeile.Montag) ||
                       !string.IsNullOrWhiteSpace(zeile.Dienstag) ||
                       !string.IsNullOrWhiteSpace(zeile.Mittwoch) ||
                       !string.IsNullOrWhiteSpace(zeile.Donnerstag) ||
                       !string.IsNullOrWhiteSpace(zeile.Freitag);
            };

            uiGrid.ItemsSource = planDaten;
        }

        // Hilfsmethode, um die Kürzel zu finden (alleLehrerDaten ist die Liste aus dem vorherigen Schritt)
        private string GetLehrerFuer(int pause, int tag, int ortIdx)
        {
            // Wir suchen alle Einträge, die exakt diese IDs haben
            var treffer = aLehrer
                .Where(l => l.PauseID == pause
                         && l.TagID == tag
                         && l.OrtID == ortIdx)
                .Select(l => l.Kuerzel);

            // Falls mehrere Lehrer am gleichen Ort sind (z.B. große Pausenhöfe),
            // werden sie mit Komma getrennt in die Zelle geschrieben.
            return string.Join(", ", treffer);
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(uiPause.SelectedItem is ComboBoxItem selectedItem)
            {
                // Den Tag (die ID) auslesen
                if (selectedItem.Tag != null && int.TryParse(selectedItem.Tag.ToString(), out int pauseId))
                {
                    // Jetzt kannst du deine Refresh-Logik mit der ID aufrufen
                    RefreshGrid(pauseId);
                }
            }
        }

        private void uiHideEmpty_Checked(object sender, RoutedEventArgs e)
        {
            if (uiPause.SelectedItem is ComboBoxItem selectedItem)
            {
                // Den Tag (die ID) auslesen
                if (selectedItem.Tag != null && int.TryParse(selectedItem.Tag.ToString(), out int pauseId))
                {
                    // Jetzt kannst du deine Refresh-Logik mit der ID aufrufen
                    RefreshGrid(pauseId);
                }
            }
        }
    }
}