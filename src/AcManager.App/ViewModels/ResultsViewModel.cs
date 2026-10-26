using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using AcManager.Core.Results;
using AcTools.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AcManager.App.ViewModels;

public partial class ResultsViewModel : ObservableObject
{
    private string? _documents;

    public ObservableCollection<string> Lines { get; } = [];

    public ObservableCollection<string> Replays { get; } = [];

    [ObservableProperty]
    private string _status = "Set the Documents directory on the Install page. Results live in that folder.";

    public void UseLayout(AcInstallLayout layout)
    {
        _documents = layout.DocumentsDirectory;
        Reload();
    }

    [RelayCommand]
    public void Reload()
    {
        Lines.Clear();
        Replays.Clear();
        if (string.IsNullOrWhiteSpace(_documents)) {
            Status = "Set the Documents directory on the Install page. Results live in that folder.";
            return;
        }

        try {
            var race = RaceOutFile.Read(_documents);
            if (race != null) {
                foreach (var line in race.Lines()) {
                    Lines.Add(line);
                }
            }

            var replays = ReplayLibrary.List(_documents);
            foreach (var replay in replays) {
                Replays.Add(replay.Label);
            }

            if (race == null && replays.Count == 0) {
                Status = "No race_out.json yet. Replays will show up here after a session.";
                return;
            }

            Status = race == null
                ? replays.Count.ToString(CultureInfo.InvariantCulture) + " replays in the Documents replay folder."
                : "Last session is in out/race_out.json. "
                    + replays.Count.ToString(CultureInfo.InvariantCulture)
                    + " replays.";
        } catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException) {
            Status = "race_out.json could not be read.";
        }
    }
}
