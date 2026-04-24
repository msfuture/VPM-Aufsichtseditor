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
using System.Collections.ObjectModel;

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
        private List<LehrerStammData> aStamm = new List<LehrerStammData>();
        private ICollectionView _planView;
        private HashSet<string> _gueltigeKuerzel = new HashSet<string>();
        private Dictionary<string, string> _kuerzelLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private List<string> _spmHeader = new List<string>();
        string openedPath;
        private PlanZeile _draggedItem;
        private ObservableCollection<PlanZeile> _planDaten = new ObservableCollection<PlanZeile>();
        public MainWindow()
        {
            InitializeComponent();
            CommandBinding pasteBinding = new CommandBinding(ApplicationCommands.Paste, OnPasteExecuted);
            uiGrid.CommandBindings.Add(pasteBinding);
        }

        private void OnPasteExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            // 1. Laufende Bearbeitungen beenden (WICHTIG gegen die Exception)
            uiGrid.CommitEdit(DataGridEditingUnit.Row, true);

            string clipboardText = Clipboard.GetText();
            if (string.IsNullOrEmpty(clipboardText)) return;

            var selectedCells = uiGrid.SelectedCells;
            if (selectedCells.Count == 0) return;

            // Startpunkte finden
            int startRowIndex = selectedCells.Min(c => uiGrid.Items.IndexOf(c.Item));
            int startColumnIndex = selectedCells.Min(c => c.Column.DisplayIndex);

            string[] lines = clipboardText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < lines.Length; i++)
            {
                int rowIndex = startRowIndex + i;
                if (rowIndex >= uiGrid.Items.Count) break;

                string[] cells = lines[i].Split('\t');
                var planZeile = uiGrid.Items[rowIndex] as PlanZeile;

                for (int j = 0; j < cells.Length; j++)
                {
                    int colIndex = startColumnIndex + j;
                    if (colIndex >= uiGrid.Columns.Count) break;

                    var column = uiGrid.Columns[colIndex];
                    if (column.IsReadOnly)
                    {
                        // Falls wir auf der Ort-Spalte starten, eins nach rechts rücken
                        if (j == 0) startColumnIndex++;
                        continue;
                    }

                    string rawValue = cells[j].Trim();
                    string korrigiertesKuerzel = "";

                    if (!string.IsNullOrEmpty(rawValue))
                    {
                        if (_kuerzelLookup.TryGetValue(rawValue, out string korrektur))
                        {
                            korrigiertesKuerzel = korrektur;
                        }
                        else
                        {
                            continue;
                        }
                    }

                    string header = column.Header.ToString();
                    SetWochentagWert(planZeile, header, korrigiertesKuerzel);
                    UpdateLehrerDaten(planZeile.OrtIndex, header, korrigiertesKuerzel);
                }
            }

            // 2. Jetzt ist Refresh sicher
            uiGrid.Items.Refresh();
        }

        // Kleine Hilfsmethode für Schritt 5
        private void SetWochentagWert(PlanZeile zeile, string tag, string kuerzel)
        {
            switch (tag)
            {
                case "Montag": zeile.Montag = kuerzel; break;
                case "Dienstag": zeile.Dienstag = kuerzel; break;
                case "Mittwoch": zeile.Mittwoch = kuerzel; break;
                case "Donnerstag": zeile.Donnerstag = kuerzel; break;
                case "Freitag": zeile.Freitag = kuerzel; break;
            }
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
                openedPath = selectedPath;
                LoadVpmData(selectedPath);
                aLehrer = ParseSpmFile(selectedPath + "\\vpm_lehrer.spm", aOrte);
                uiSave.IsEnabled = true;
                uiPause.IsEnabled = true;
                uiHideEmpty.IsEnabled = true;
                uiPause.SelectedIndex = 0; // Automatisch die erste Pause auswählen und anzeigen
                RefreshGrid(1);
            }
        }

        private void LoadVpmData(string pfad)
        {
            aOrte = GetAufsichtsorte(pfad);
        }

        public List<AufsichtZeile> ParseSpmFile(string filePath, string[] aufsichtsOrte)
        {
            var liste = new List<AufsichtZeile>();
            _spmHeader.Clear();
            aStamm.Clear();
            var lines = File.ReadAllLines(filePath, Encoding.Latin1);

            foreach (var line in lines)
            {
                // Header-Zeilen identifizieren
                if (line.StartsWith("//"))
                {
                    _spmHeader.Add(line);
                    continue; // Nächste Zeile
                }
                var parts = line.Split('\t');
                if (parts.Length >= 6)
                {
                    string kuerzel = parts[0];
                    string nachname = parts[1];
                    string spaltenInhalt = parts[5]; // Die Spalte mit "2-4-15,1-2-3"
                    _gueltigeKuerzel.Add(kuerzel);
                    _kuerzelLookup[kuerzel] = kuerzel;
                    // LehrerStammData erstellen und zur Liste hinzufügen
                    var lehrerStamm = new LehrerStammData
                    {
                        Kuerzel = kuerzel,
                        Nachname = nachname,
                        OriginalZeilenArray = parts
                    };
                    aStamm.Add(lehrerStamm);
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
            // 1. Die gespeicherte Reihenfolge laden
            List<string> gespeicherteReihenfolge = LoadCustomSortOrder(openedPath);

            // 2. Das String-Array der Orte sortieren
            List<string> sortierteOrte;

            if (gespeicherteReihenfolge != null && gespeicherteReihenfolge.Count > 0)
            {
                sortierteOrte = aOrte.OrderBy(ort => {
                    int index = gespeicherteReihenfolge.IndexOf(ort);
                    // Wenn der Ort nicht in der Sortierdatei steht, kriegt er einen hohen Index (unten anfügen)
                    return index == -1 ? int.MaxValue : index;
                }).ToList();
            }
            else
            {
                // Falls keine Sortierdatei da ist, einfach so lassen wie sie kommen
                sortierteOrte = aOrte.ToList();
            }

            _planDaten = new ObservableCollection<PlanZeile>();

            // Wir gehen alle Orte aus deinem INI-Array durch
            foreach (var ortName in sortierteOrte)
            {
                var zeile = new PlanZeile
                {
                    Ort = ortName,
                    OrtIndex = Array.IndexOf(aOrte, ortName) + 1 // VPM ist 1-basiert, unser Array ist 0-basiert
                };

                // Jetzt suchen wir in den eingelesenen SPM-Daten nach Lehrern, 
                // die in DIESER Pause an DIESEM Ort eingeteilt sind.
                zeile.Montag = GetLehrerFuer(pause, 1, zeile.OrtIndex);
                zeile.Dienstag = GetLehrerFuer(pause, 2, zeile.OrtIndex);
                zeile.Mittwoch = GetLehrerFuer(pause, 3, zeile.OrtIndex);
                zeile.Donnerstag = GetLehrerFuer(pause, 4, zeile.OrtIndex);
                zeile.Freitag = GetLehrerFuer(pause, 5, zeile.OrtIndex);

                _planDaten.Add(zeile);
            }

            _planView = CollectionViewSource.GetDefaultView(_planDaten);

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

            uiGrid.ItemsSource = _planDaten;
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

        private void uiGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditingElement is TextBox editBox)
            {
                string eingabeRaw = editBox.Text.Trim();
                if (string.IsNullOrEmpty(eingabeRaw))
                {
                    // Löschen der Zelle erlauben
                    UpdateLehrerDatenFromGrid(e);
                    return;
                }

                // Mehrere Kürzel verarbeiten (falls vorhanden)
                var teile = eingabeRaw.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                      .Select(p => p.Trim())
                                      .ToList();

                List<string> korrigierteListe = new List<string>();

                foreach (var k in teile)
                {
                    // 1. Genaue Übereinstimmung prüfen
                    if (_gueltigeKuerzel.Contains(k))
                    {
                        korrigierteListe.Add(k);
                    }
                    // 2. Case-Insensitive Übereinstimmung prüfen und korrigieren
                    else if (_kuerzelLookup.TryGetValue(k, out string korrektur))
                    {
                        korrigierteListe.Add(korrektur);
                    }
                    // 3. Gar nicht gefunden
                    else
                    {
                        MessageBox.Show($"Das Kürzel '{k}' ist unbekannt. Bitte ggf. Lehrkräfte in VPM prüfen.", "Fehlerhafte Eingabe", MessageBoxButton.OK, MessageBoxImage.Error);
                        e.Cancel = true; // Abbruch der Bearbeitung
                        return;
                    }
                }

                // Die korrigierten Kürzel zurück in die TextBox schreiben (visuelles Feedback)
                string finalerString = string.Join(", ", korrigierteListe);
                editBox.Text = finalerString;

                // In der Logik-Liste speichern
                UpdateLehrerDatenFromGrid(e, finalerString);
            }
        }

        private void UpdateLehrerDatenFromGrid(DataGridCellEditEndingEventArgs e, string kuerzelString = "")
        {
            var zeile = e.Row.Item as PlanZeile;
            string wochentag = e.Column.Header.ToString();

            if (zeile != null)
            {
                UpdateLehrerDaten(zeile.OrtIndex, wochentag, kuerzelString);
            }
        }

        private void UpdateLehrerDaten(int ortId, string tagName, string kuerzel)
        {
            if (uiPause.SelectedItem is ComboBoxItem selectedItem &&
        selectedItem.Tag != null &&
        int.TryParse(selectedItem.Tag.ToString(), out int pauseId))
            {
                int tagId = GetTagIdFromName(tagName);

                // Alte Einträge löschen
                aLehrer.RemoveAll(l => l.OrtID == ortId && l.TagID == tagId && l.PauseID == pauseId);

                if (!string.IsNullOrEmpty(kuerzel))
                {
                    var kuerzelListe = kuerzel.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var k in kuerzelListe)
                    {
                        string sauber = k.Trim(); // Case bleibt erhalten!
                        if (_gueltigeKuerzel.Contains(sauber))
                        {
                            aLehrer.Add(new AufsichtZeile
                            {
                                Kuerzel = sauber,
                                OrtID = ortId,
                                TagID = tagId,
                                PauseID = pauseId
                            });
                        }
                    }
                }
            }
        }

        private int GetTagIdFromName(string name)
        {
            return name switch
            {
                "Montag" => 1,
                "Dienstag" => 2,
                "Mittwoch" => 3,
                "Donnerstag" => 4,
                "Freitag" => 5,
                _ => 0
            };
        }

        public void ExportSpmFile(string folderPath)
        {
            string originalFilePath = Path.Combine(folderPath, "vpm_lehrer.spm");
            if (!File.Exists(originalFilePath))
            {
                MessageBox.Show("Die Originaldatei wurde im angegebenen Ordner nicht gefunden.", "Fehler");
                return;
            }

            // 2. Sicherheits-Backup erstellen (vpm_lehrer.spm.bak)
            string backupPath = originalFilePath + ".bak";
            File.Copy(originalFilePath, backupPath, true);

            using (StreamWriter writer = new StreamWriter(originalFilePath, false, Encoding.Latin1))
            {
                // 1. Header
                foreach (var header in _spmHeader) writer.WriteLine(header);

                // 2. Alle Lehrer aus der Stammliste durchgehen
                foreach (var stamm in aStamm)
                {
                    // Alle aktuellen Aufsichten für DIESES Kürzel sammeln
                    var codes = aLehrer
                        .Where(a => a.Kuerzel == stamm.Kuerzel)
                        .Select(a => $"{a.PauseID}-{a.TagID}-{a.OrtID}");

                    string neuerCodeString = string.Join(",", codes);

                    // Schablone nehmen und Spalte 6 aktualisieren
                    string[] ausgabeZeile = (string[])stamm.OriginalZeilenArray.Clone();
                    ausgabeZeile[5] = neuerCodeString;

                    // Als Tab-getrennte Zeile schreiben
                    writer.WriteLine(string.Join("\t", ausgabeZeile));
                }
            }
            MessageBox.Show("Daten wurden erfolgreich gespeichert und ein Backup erstellt.", "Speichern");
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            ExportSpmFile(openedPath);
        }

        private static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T parent) return parent;
                child = VisualTreeHelper.GetParent(child);
            }
            return null;
        }

        private void uiGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var element = e.OriginalSource as DependencyObject;
            var row = FindVisualParent<DataGridRow>(element);
            var cell = FindVisualParent<DataGridCell>(element);

            // Drag nur starten, wenn wir auf dem RowHeader ODER der ersten Spalte sind
            if (row != null && (cell == null || cell.Column.DisplayIndex == 0))
            {
                _draggedItem = row.Item as PlanZeile;

                if (_draggedItem != null)
                {
                    // Markiert die Zeile visuell, damit man sieht, was man zieht
                    uiGrid.SelectedItem = _draggedItem;

                    DragDrop.DoDragDrop(row, _draggedItem, DragDropEffects.Move);

                    // Wichtig: Verhindert, dass das Grid denkt, wir wollten nur eine Zelle markieren
                    e.Handled = true;
                }
            }
        }

        private void uiGrid_Drop(object sender, DragEventArgs e)
        {
            if (_draggedItem == null) return;

            // 1. Position der Maus relativ zum DataGrid bestimmen
            Point dropPosition = e.GetPosition(uiGrid);

            // 2. Das Element an dieser Position finden
            IInputElement element = uiGrid.InputHitTest(dropPosition);
            if (element == null) return;

            // 3. Den Visual Parent (die Zeile) suchen
            DataGridRow targetRow = FindVisualParent<DataGridRow>(element as DependencyObject);

            if (targetRow != null)
            {
                PlanZeile targetItem = targetRow.Item as PlanZeile;

                if (targetItem != null && _draggedItem != targetItem)
                {
                    int oldIndex = _planDaten.IndexOf(_draggedItem);
                    int newIndex = _planDaten.IndexOf(targetItem);

                    if (oldIndex != -1 && newIndex != -1)
                    {
                        // Die ObservableCollection sortiert sich hier um
                        _planDaten.Move(oldIndex, newIndex);

                        // Fokus auf das verschobene Item setzen
                        uiGrid.SelectedItem = _draggedItem;

                        // Speichern der neuen Sortierung
                        SaveCustomSortOrder(openedPath);
                    }
                }
            }

            _draggedItem = null;
            e.Handled = true; // Wichtig: Event als erledigt markieren
        }

        private void uiGrid_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }

        private void SaveCustomSortOrder(string folderPath)
        {
            string path = Path.Combine(folderPath, "ort_sortierung.txt");
            // Wir speichern nur die Namen der Orte in der aktuellen Reihenfolge
            var namen = _planDaten.Select(z => z.Ort);
            File.WriteAllLines(path, namen);
        }

        private List<string> LoadCustomSortOrder(string folderPath)
        {
            string path = Path.Combine(folderPath, "ort_sortierung.txt");
            if (File.Exists(path))
            {
                return File.ReadAllLines(path).ToList();
            }
            return null;
        }
    }
}