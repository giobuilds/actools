using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.Http;
using System.Text;
using AcManager.Core.Content;
using AcManager.Core.Online;
using AcTools.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AcManager.App.ViewModels;

public partial class OnlineViewModel : ObservableObject
{
    private readonly IGameLauncher _launcher;
    private readonly IOnlineDirectory _directory;
    private readonly ISteamIdLookup _steamIds;
    private readonly IServerContentInstaller _content;
    private readonly List<LobbyServer> _servers = [];
    private readonly List<LobbyServer> _added = [];
    private AcInstallLayout _layout = new(null, null, null, "");
    private IReadOnlyList<CarSummary> _localCars = [];
    private IReadOnlyList<TrackSummary> _localTracks = [];
    private IReadOnlyList<ServerContentOffer> _offers = [];
    private string? _flagsAddress;
    private bool _extended;
    private string? _features;
    private CancellationTokenSource? _fetch;
    private CancellationTokenSource? _watch;
    private CancellationTokenSource? _details;
    private int _detailsGeneration;
    private DetailsState _detailsState = DetailsState.None;
    private string? _detailsNote;

    private enum DetailsState
    {
        None,
        Loading,
        Ready,
        Failed,
    }

    public ObservableCollection<LobbyServer> FilteredServers { get; } = [];

    public ObservableCollection<CarSummary> Cars { get; } = [];

    [ObservableProperty]
    private string _filter = "";

    [ObservableProperty]
    private LobbyServer? _selectedServer;

    [ObservableProperty]
    private CarSummary? _selectedCar;

    [ObservableProperty]
    private string _address = "";

    [ObservableProperty]
    private string _driverName = "Player";

    [ObservableProperty]
    private string _password = "";

    [ObservableProperty]
    private string _status = "Refresh loads the public lobby. Add a server as ip:httpPort when the list is unavailable.";

    [ObservableProperty]
    private bool _busy;

    public OnlineViewModel(
        IGameLauncher launcher,
        IOnlineDirectory directory,
        ISteamIdLookup steamIds,
        IServerContentInstaller? content = null)
    {
        _launcher = launcher;
        _directory = directory;
        _steamIds = steamIds;
        _content = content ?? new ServerContentInstaller();
        var persona = CleanPersona(steamIds.PersonaName());
        if (persona != null) {
            DriverName = persona;
        }
    }

    private static string? CleanPersona(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) {
            return null;
        }

        var builder = new StringBuilder();
        foreach (var c in name.Trim()) {
            if (c is '\r' or '\n' or '\0') {
                continue;
            }

            builder.Append(c);
            if (builder.Length >= 80) {
                break;
            }
        }

        return builder.Length == 0 ? null : builder.ToString();
    }

    public Action? Installed { get; set; }

    public string Details
    {
        get {
            var server = SelectedServer;
            if (server == null) {
                return "Pick a server from the list, or add one by address.";
            }

            var track = string.IsNullOrWhiteSpace(server.Track) ? "unknown track" : server.Track;
            var race = server.RacePort > 0
                ? server.RacePort.ToString(CultureInfo.InvariantCulture)
                : "missing";
            var password = server.PasswordRequired ? " Password required." : "";
            return track + "  ·  " + server.PlayersLabel + "  ·  race port " + race + "  ·  " + server.Address + "." + password;
        }
    }

    public string JoinHint
    {
        get {
            var parts = new List<string>();
            if (string.IsNullOrWhiteSpace(_layout.DocumentsDirectory)) {
                parts.Add("Set the Documents directory on the Install page so race.ini has a place to go.");
            } else if (SelectedServer != null && SelectedServer.RacePort <= 0) {
                parts.Add("This server has no race port, so it cannot be joined.");
            } else if (SelectedServer != null && _localCars.Count == 0) {
                parts.Add("The cars folder is empty.");
            } else if (SelectedServer != null && Cars.Count == 0) {
                parts.Add("None of this server's cars are installed.");
            }

            var missing = DescribeMissing();
            if (missing.Length > 0) {
                parts.Add(missing);
            }

            return string.Join(" ", parts);
        }
    }

    public void UseLayout(AcInstallLayout layout)
    {
        _layout = layout;
        Reload();
        OnPropertyChanged(nameof(JoinHint));
        JoinCommand.NotifyCanExecuteChanged();
        InstallMissingCommand.NotifyCanExecuteChanged();
    }

    public void Reload()
    {
        _localCars = ContentLibrary.ScanCars(_layout.CarsDirectory);
        _localTracks = ContentLibrary.ScanTracks(_layout.TracksDirectory);
        RebuildCars();
    }

    partial void OnFilterChanged(string value) => ApplyFilter();

    partial void OnSelectedServerChanged(LobbyServer? value)
    {
        RebuildCars();
        OnPropertyChanged(nameof(Details));
        OnPropertyChanged(nameof(JoinHint));
        JoinCommand.NotifyCanExecuteChanged();
        _ = LoadDetailsAsync(value);
    }

    partial void OnSelectedCarChanged(CarSummary? value)
    {
        JoinCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(JoinHint));
    }

    partial void OnBusyChanged(bool value)
    {
        RefreshCommand.NotifyCanExecuteChanged();
        AddCommand.NotifyCanExecuteChanged();
        JoinCommand.NotifyCanExecuteChanged();
        InstallMissingCommand.NotifyCanExecuteChanged();
    }

    private bool CanRefresh() => !Busy;

    private bool CanAdd() => !Busy;

    private bool CanJoin() =>
        !Busy
        && SelectedServer != null
        && SelectedServer.RacePort > 0
        && SelectedCar != null
        && !string.IsNullOrWhiteSpace(_layout.DocumentsDirectory);

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private async Task Refresh()
    {
        var token = BeginFetch();
        Status = "Loading the server list.";
        try {
            var result = await _directory.LoadListAsync(token);
            if (token.IsCancellationRequested) {
                return;
            }

            if (!result.Ok) {
                Status = result.Error ?? "The lobby list did not return a server list.";
                return;
            }

            ReplaceFromList(result.Servers);
        } catch (OperationCanceledException) {
            // A newer load replaced this one.
        } finally {
            EndFetch(token);
        }
    }

    public Task OpenInviteAsync(OnlineInvite invite)
    {
        Address = invite.Ip + ":" + invite.HttpPort.ToString(CultureInfo.InvariantCulture);
        if (!string.IsNullOrEmpty(invite.Password)) {
            Password = invite.Password;
        }

        return Add();
    }

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private async Task Add()
    {
        if (!LobbyAddress.TryParse(Address, out var ip, out var httpPort)) {
            Status = "Enter an address as ip:httpPort.";
            return;
        }

        var token = BeginFetch();
        Status = "Loading " + ip + ":" + httpPort.ToString(CultureInfo.InvariantCulture) + ".";
        try {
            var result = await _directory.LoadServerAsync(ip, httpPort, token);
            if (token.IsCancellationRequested) {
                return;
            }

            if (!result.Ok || result.Servers.Count == 0) {
                Status = result.Error ?? "The server did not return session info.";
                return;
            }

            Upsert(result.Servers[0]);
        } catch (OperationCanceledException) {
            // A newer load replaced this one.
        } finally {
            EndFetch(token);
        }
    }

    private bool CanInstall() => !Busy && MissingOffers().Count > 0 && _layout.HasGameRoot;

    [RelayCommand(CanExecute = nameof(CanInstall))]
    private async Task InstallMissing()
    {
        var root = _layout.GameRoot;
        var offers = MissingOffers();
        if (string.IsNullOrWhiteSpace(root) || offers.Count == 0) {
            return;
        }

        Busy = true;
        SetStatus("Installing missing content.");
        try {
            var result = await _content.InstallAsync(offers, root, ReportInstall, CancellationToken.None);
            Reload();
            Installed?.Invoke();
            SetStatus(InstalledStatus(result));
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            SetStatus(ex.Message);
        } finally {
            Busy = false;
            NotifyContent();
        }
    }

    [RelayCommand(CanExecute = nameof(CanJoin))]
    private async Task Join()
    {
        var server = SelectedServer;
        var car = SelectedCar;
        if (server == null || car == null || string.IsNullOrWhiteSpace(_layout.DocumentsDirectory)) {
            return;
        }

        if (server.RacePort <= 0) {
            Status = "This server has no race port, so it cannot be joined.";
            return;
        }

        if (_flagsAddress != server.Address) {
            await LoadDetailsAsync(server);
        }

        if (!ReferenceEquals(SelectedServer, server) || SelectedCar?.Id != car.Id) {
            return;
        }

        var extended = _flagsAddress == server.Address && _extended;
        var features = _flagsAddress == server.Address ? _features : null;
        try {
            var steamId = _steamIds.MostRecentId();
            var path = OnlineSessionFile.Write(_layout.DocumentsDirectory, new OnlineJoin(
                server.Ip,
                server.HttpPort,
                server.RacePort,
                server.Name,
                car.Id,
                Password,
                DriverName,
                steamId,
                extended,
                features));
            var launch = _launcher.LaunchSession(_layout);
            var steamNote = SteamIdText.IsSeventeenDigits(steamId)
                ? ""
                : " No Steam ID was found in the local Steam login, so the server may reject the session.";
            Status = LaunchFeedback.AfterWrite(_launcher, launch, path, "Starting the online session.") + steamNote;
            if (launch == GameLaunchStatus.SessionStarted) {
                WatchForGame();
            }
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException) {
            Status = ex.Message;
        }
    }

    private CancellationToken BeginFetch()
    {
        _fetch?.Cancel();
        _fetch = new CancellationTokenSource();
        Busy = true;
        return _fetch.Token;
    }

    private void EndFetch(CancellationToken token)
    {
        if (token.IsCancellationRequested) {
            return;
        }

        Busy = false;
    }

    private void ReplaceFromList(IReadOnlyList<LobbyServer> servers)
    {
        for (var i = _added.Count - 1; i >= 0; i--) {
            foreach (var server in servers) {
                if (server.Address == _added[i].Address) {
                    _added.RemoveAt(i);
                    break;
                }
            }
        }

        _servers.Clear();
        foreach (var server in servers) {
            _servers.Add(server);
        }

        foreach (var extra in _added) {
            _servers.Add(extra);
        }

        ApplyFilter();
        if (servers.Count == 0 && _added.Count == 0) {
            Status = "The lobby list is empty.";
        } else if (servers.Count == 0) {
            Status = "The lobby list is empty. Showing servers added by address.";
        } else if (_servers.Count == 1) {
            Status = "1 server.";
        } else {
            Status = _servers.Count.ToString(CultureInfo.InvariantCulture) + " servers.";
        }
    }

    private void Upsert(LobbyServer server)
    {
        ReplaceAddress(_added, server);
        ReplaceAddress(_servers, server);
        ApplyFilter();
        var visible = false;
        foreach (var item in FilteredServers) {
            if (item.Address == server.Address) {
                SelectedServer = item;
                visible = true;
                break;
            }
        }

        Status = visible
            ? "Added " + server.Title + "."
            : "Added " + server.Title + ". The current filter hides it.";
    }

    private static void ReplaceAddress(List<LobbyServer> servers, LobbyServer server)
    {
        for (var i = 0; i < servers.Count; i++) {
            if (servers[i].Address == server.Address) {
                servers[i] = server;
                return;
            }
        }

        servers.Add(server);
    }

    private void ApplyFilter()
    {
        var selectedAddress = SelectedServer?.Address;
        FilteredServers.Clear();
        foreach (var server in _servers) {
            if (Matches(server, Filter)) {
                FilteredServers.Add(server);
            }
        }

        LobbyServer? next = null;
        if (selectedAddress != null) {
            foreach (var server in FilteredServers) {
                if (server.Address == selectedAddress) {
                    next = server;
                    break;
                }
            }
        }

        SelectedServer = next ?? (FilteredServers.Count == 0 ? null : FilteredServers[0]);
    }

    private static bool Matches(LobbyServer server, string filter)
    {
        if (string.IsNullOrWhiteSpace(filter)) {
            return true;
        }

        if (server.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || server.Track.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || server.Ip.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || server.Address.Contains(filter, StringComparison.OrdinalIgnoreCase)) {
            return true;
        }

        foreach (var car in server.Cars) {
            if (car.Contains(filter, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
        }

        return false;
    }

    private void RebuildCars()
    {
        var selectedId = SelectedCar?.Id;
        Cars.Clear();
        var server = SelectedServer;
        if (server != null) {
            foreach (var car in _localCars) {
                if (ServerAllows(server, car.Id)) {
                    Cars.Add(car);
                }
            }
        }

        SelectedCar = null;
        foreach (var car in Cars) {
            if (car.Id == selectedId) {
                SelectedCar = car;
                break;
            }
        }

        SelectedCar ??= Cars.Count == 0 ? null : Cars[0];
        OnPropertyChanged(nameof(JoinHint));
        JoinCommand.NotifyCanExecuteChanged();
        InstallMissingCommand.NotifyCanExecuteChanged();
    }

    private void RememberFlags(LobbyServer server, string json)
    {
        var flags = ServerSessionFlagsJson.Read(json);
        _flagsAddress = server.Address;
        _extended = flags.Extended;
        _features = flags.Features;
    }

    private async Task LoadDetailsAsync(LobbyServer? server)
    {
        var previous = _details;
        _details = new CancellationTokenSource();
        previous?.Cancel();
        var token = _details.Token;
        var generation = ++_detailsGeneration;
        _offers = [];
        _detailsNote = null;
        _flagsAddress = null;
        _extended = false;
        _features = null;
        _detailsState = server == null ? DetailsState.None : DetailsState.Loading;
        NotifyContent();
        if (server == null) {
            return;
        }

        try {
            var result = await _directory.LoadDetailsAsync(server.Ip, server.HttpPort, token);
            if (generation != _detailsGeneration || token.IsCancellationRequested) {
                return;
            }

            if (!result.Ok || result.Json == null) {
                _detailsState = DetailsState.Failed;
                _detailsNote = result.Error ?? "The server did not publish downloads.";
            } else {
                RememberFlags(server, result.Json);
                if (!ServerContentJson.TryRead(result.Json, server.Track, out var offers, out var error)) {
                    _detailsState = DetailsState.Failed;
                    _detailsNote = string.IsNullOrEmpty(error) ? "The server did not publish downloads." : error;
                } else if (offers.Count == 0) {
                    _offers = offers;
                    _detailsState = DetailsState.Ready;
                    _detailsNote = "The server did not publish downloads.";
                } else {
                    _offers = offers;
                    _detailsState = DetailsState.Ready;
                    _detailsNote = null;
                }
            }
        } catch (OperationCanceledException) {
            return;
        } catch (ObjectDisposedException) {
            return;
        } catch (Exception ex) when (ex is HttpRequestException or IOException) {
            if (generation != _detailsGeneration) {
                return;
            }

            _detailsState = DetailsState.Failed;
            _detailsNote = "Could not reach that server.";
        }

        if (generation == _detailsGeneration) {
            NotifyContent();
        }
    }

    private string DescribeMissing()
    {
        var server = SelectedServer;
        if (server == null) {
            return "";
        }

        var sentences = new List<string>();
        var cars = MissingCarIds(server);
        if (cars.Count == 1) {
            sentences.Add("Missing car " + cars[0] + ".");
        } else if (cars.Count > 1) {
            var shown = Math.Min(3, cars.Count);
            var names = new List<string>();
            for (var i = 0; i < shown; i++) {
                names.Add(cars[i]);
            }

            var list = string.Join(", ", names);
            if (cars.Count > shown) {
                var more = (cars.Count - shown).ToString(CultureInfo.InvariantCulture);
                sentences.Add("Missing cars " + list + ", and " + more + " more.");
            } else {
                sentences.Add("Missing cars " + list + ".");
            }
        }

        var track = MissingTrackId(server);
        if (track != null) {
            sentences.Add("Missing track " + track + ".");
        }

        if (sentences.Count == 0) {
            return "";
        }

        if (_detailsState == DetailsState.Loading) {
            sentences.Add("Checking this server for downloads.");
        } else if (!string.IsNullOrEmpty(_detailsNote)) {
            var note = _detailsNote;
            if (!note.EndsWith('.')) {
                note += ".";
            }

            sentences.Add(note);
        }

        return string.Join(" ", sentences);
    }

    private List<ServerContentOffer> MissingOffers()
    {
        var result = new List<ServerContentOffer>();
        var server = SelectedServer;
        if (server == null || _detailsState != DetailsState.Ready) {
            return result;
        }

        foreach (var offer in _offers) {
            if (offer.Kind == InstalledContentKind.Car) {
                if (server.Cars.Count > 0 && !ServerAllows(server, offer.Id)) {
                    continue;
                }

                if (server.Cars.Count == 0) {
                    continue;
                }

                if (!IsMissingCar(offer.Id)) {
                    continue;
                }
            } else if (!IsMissingTrack(offer.Id)) {
                continue;
            }

            if (!ContentDownloadUrl.IsAllowed(offer.Url)) {
                continue;
            }

            result.Add(offer);
        }

        return result;
    }

    private List<string> MissingCarIds(LobbyServer server)
    {
        var missing = new List<string>();
        foreach (var id in server.Cars) {
            if (IsMissingCar(id)) {
                missing.Add(id);
            }
        }

        return missing;
    }

    private bool IsMissingCar(string id)
    {
        foreach (var car in _localCars) {
            if (car.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) {
                return false;
            }
        }

        return true;
    }

    private string? MissingTrackId(LobbyServer server)
    {
        var id = ServerContentJson.TrackFolderId(server.Track);
        if (id == null || !IsMissingTrack(id)) {
            return null;
        }

        return id;
    }

    private bool IsMissingTrack(string id)
    {
        foreach (var track in _localTracks) {
            if (track.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) {
                return false;
            }
        }

        return true;
    }

    private int _statusGeneration;

    private void ReportInstall(ServerContentProgress progress)
    {
        SetStatus(progress.Phase
            + " "
            + progress.Index.ToString(CultureInfo.InvariantCulture)
            + " of "
            + progress.Count.ToString(CultureInfo.InvariantCulture)
            + ", "
            + progress.Id
            + ".");
    }

    private void SetStatus(string text)
    {
        var generation = Interlocked.Increment(ref _statusGeneration);
        void Apply()
        {
            if (generation == Volatile.Read(ref _statusGeneration)) {
                Status = text;
            }
        }

        if (Dispatcher.UIThread.CheckAccess()) {
            Apply();
            return;
        }

        Apply();
        try {
            Dispatcher.UIThread.Post(Apply);
        } catch (InvalidOperationException) {
        }
    }

    private static string InstalledStatus(ContentInstallResult result)
    {
        if (result.Installed.Count == 0) {
            return result.Skipped.Count == 0
                ? "Nothing was installed."
                : string.Join(" ", result.Skipped);
        }

        var names = new List<string>();
        foreach (var piece in result.Installed) {
            names.Add(piece.Kind + " " + piece.Id);
        }

        var text = "Installed " + string.Join(", ", names) + ".";
        if (result.Skipped.Count > 0) {
            text += " " + string.Join(" ", result.Skipped);
        }

        return text;
    }

    private void NotifyContent()
    {
        OnPropertyChanged(nameof(JoinHint));
        InstallMissingCommand.NotifyCanExecuteChanged();
        JoinCommand.NotifyCanExecuteChanged();
    }

    private static bool ServerAllows(LobbyServer server, string carId)
    {
        if (server.Cars.Count == 0) {
            return true;
        }

        foreach (var id in server.Cars) {
            if (id.Equals(carId, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
        }

        return false;
    }

    [RelayCommand]
    private void OpenLaunchLog()
    {
        var message = LaunchFeedback.TryOpen(_launcher);
        if (message != null) {
            Status = message;
        }
    }

    private void WatchForGame()
    {
        _watch?.Cancel();
        _watch = new CancellationTokenSource();
        var token = _watch.Token;
        _ = Task.Run(async () => {
            try {
                var sawGame = false;
                for (var i = 0; i < 60 && !token.IsCancellationRequested && !sawGame; i++) {
                    await Task.Delay(TimeSpan.FromSeconds(1), token).ConfigureAwait(false);
                    if (_launcher.IsGameRunning()) {
                        sawGame = true;
                        Post(token, "Assetto Corsa is running.");
                    }
                }

                while (sawGame && !token.IsCancellationRequested) {
                    await Task.Delay(TimeSpan.FromSeconds(1), token).ConfigureAwait(false);
                    if (!_launcher.IsGameRunning()) {
                        Post(token, "Assetto Corsa exited.");
                        return;
                    }
                }

                if (!sawGame) {
                    Post(token, LaunchFeedback.GaveUp(_launcher));
                }
            } catch (OperationCanceledException) {
                // A newer join replaced this watch.
            }
        }, token);
    }

    private void Post(CancellationToken token, string status)
    {
        if (token.IsCancellationRequested) {
            return;
        }

        Dispatcher.UIThread.Post(() => {
            if (!token.IsCancellationRequested) {
                Status = status;
            }
        });
    }
}
