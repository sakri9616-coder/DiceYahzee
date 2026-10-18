using System;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace ParvaMirabilia.Presentation
{
    public enum DuelNetworkAction { Reroll, Submit, Continue, FinishRerolls, Rematch }

    [Serializable]
    public sealed class DuelNetworkCommand
    {
        public int Round;
        public long Sequence;
        public DuelNetworkAction Action;
        public int HeldMask;
        public int Kind;
        public bool Carry;
        // Identifies which match a command belongs to, so stale rematch consents or
        // pre-restart commands can never leak into a subsequent duel on the same session.
        public int MatchGeneration;
    }

    /// <summary>Private two-player Relay session. The game owner validates all commands in VersusDuel.</summary>
    public sealed class DuelNetworkSession : MonoBehaviour
    {
        private const string Hello = "parva.duel.v1.hello";
        private const string Welcome = "parva.duel.v1.welcome";
        private const string Command = "parva.duel.v1.command";
        private const string Snapshot = "parva.duel.v1.snapshot";
        private const int MaxPayload = 24576;
        private static readonly string DefaultAuthenticationProfile = "dy" + Guid.NewGuid().ToString("N").Substring(0, 12);
        private NetworkManager manager;
        private ISession session;
        private bool handlersRegistered, leaving, busy, interrupted;
        private ulong? guest;
        private readonly long[] lastSequence = { 0, 0 };
        private float nextHello;

        // Optional stable profile; each process otherwise uses its own anonymous identity.
        public string AuthenticationProfile { get; set; }

        public bool IsHost { get; private set; }
        public bool IsReady { get; private set; }
        public int LocalPlayerIndex => IsHost ? 0 : 1;
        public string JoinCode => session?.Code ?? string.Empty;
        public event Action Joined;
        public event Action Ready;
        public event Action<int, DuelNetworkCommand> CommandReceived;
        public event Action<string> SnapshotReceived;
        public event Action<string> Interrupted;

        public Task CreateAsync() => OpenAsync(null);
        public Task JoinAsync(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("招待コードを入力してください。", nameof(code));
            return OpenAsync(code.Trim().ToUpperInvariant());
        }

        private async Task OpenAsync(string code)
        {
            if (busy || session != null || manager != null) throw new InvalidOperationException("既に接続処理中です。");
            if (string.IsNullOrWhiteSpace(AuthenticationProfile)) AuthenticationProfile = DefaultAuthenticationProfile;
            busy = true;
            leaving = false;
            interrupted = false;
            IsHost = code == null;
            guest = null;
            lastSequence[0] = lastSequence[1] = 0;
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                {
                    var options = new InitializationOptions();
                    if (!string.IsNullOrWhiteSpace(AuthenticationProfile)) options.SetProfile(AuthenticationProfile);
                    await UnityServices.InitializeAsync(options);
                }
                else if (!string.IsNullOrWhiteSpace(AuthenticationProfile) && !AuthenticationService.Instance.IsSignedIn)
                    AuthenticationService.Instance.SwitchProfile(AuthenticationProfile);
                if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
                if (leaving || this == null) throw new OperationCanceledException("対戦への接続を取り消しました。");
                if (NetworkManager.Singleton != null) throw new InvalidOperationException("別のネットワーク接続が使用中です。");
                var runtime = new GameObject("Duel Network Runtime");
                manager = runtime.AddComponent<NetworkManager>();
                var transport = runtime.AddComponent<UnityTransport>();
                manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport, EnableSceneManagement = false, ConnectionApproval = true };
                manager.ConnectionApprovalCallback = Approve;
                manager.OnClientDisconnectCallback += OnDisconnected;
                manager.OnTransportFailure += OnTransportFailure;
                session = IsHost
                    ? await MultiplayerService.Instance.CreateSessionAsync(new SessionOptions { MaxPlayers = 2, IsPrivate = true }.WithRelayNetwork())
                    : await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
                if (leaving || this == null) throw new OperationCanceledException("対戦への接続を取り消しました。");
                Joined?.Invoke();
            }
            catch
            {
                await LeaveAsync();
                throw;
            }
            finally { busy = false; }
        }

        private void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            response.Approved = !interrupted && (request.ClientNetworkId == NetworkManager.ServerClientId || manager.ConnectedClientsIds.Count < 2);
            response.CreatePlayerObject = false;
            response.Pending = false;
            response.Reason = response.Approved ? string.Empty : "この対戦には参加できません。";
        }

        private void Update()
        {
            if (manager == null || !manager.IsListening || interrupted) return;
            if (!handlersRegistered && manager.CustomMessagingManager != null)
            {
                manager.CustomMessagingManager.RegisterNamedMessageHandler(Hello, OnHello);
                manager.CustomMessagingManager.RegisterNamedMessageHandler(Welcome, OnWelcome);
                manager.CustomMessagingManager.RegisterNamedMessageHandler(Command, OnCommand);
                manager.CustomMessagingManager.RegisterNamedMessageHandler(Snapshot, OnSnapshot);
                handlersRegistered = true;
            }
            // Handshake retries also cover the host not having installed its handlers yet.
            if (handlersRegistered && !IsHost && !IsReady && manager.IsConnectedClient && Time.unscaledTime >= nextHello)
            {
                nextHello = Time.unscaledTime + 1f;
                Send(Hello, NetworkManager.ServerClientId, "1");
            }
        }

        private void OnHello(ulong sender, FastBufferReader reader)
        {
            if (!IsHost || interrupted || sender == NetworkManager.ServerClientId || !manager.ConnectedClients.ContainsKey(sender)) return;
            if (guest.HasValue && guest.Value != sender) return;
            guest = sender;
            Send(Welcome, sender, "1");
            MarkReady();
        }

        private void OnWelcome(ulong sender, FastBufferReader reader)
        {
            if (!IsHost && sender == NetworkManager.ServerClientId && !interrupted) MarkReady();
        }

        private void MarkReady()
        {
            if (IsReady) return;
            IsReady = true;
            Ready?.Invoke();
        }

        public void SendCommand(DuelNetworkCommand command)
        {
            if (!IsReady || interrupted || command == null) return;
            if (IsHost) AcceptCommand(0, command);
            else Send(Command, NetworkManager.ServerClientId, JsonUtility.ToJson(command));
        }

        private void OnCommand(ulong sender, FastBufferReader reader)
        {
            if (!IsHost || !IsReady || interrupted || !guest.HasValue || sender != guest.Value) return;
            try { AcceptCommand(1, JsonUtility.FromJson<DuelNetworkCommand>(Read(reader))); }
            catch (Exception ex) when (ex is ArgumentException || ex is OverflowException || ex is InvalidCastException) { /* malformed peer packet */ }
        }

        private void AcceptCommand(int player, DuelNetworkCommand command)
        {
            if (command == null || command.Sequence <= lastSequence[player] || command.Round < 1 || command.HeldMask < 0 || command.HeldMask > 31 || command.MatchGeneration < 0 ||
                (command.Action != DuelNetworkAction.Reroll && command.Action != DuelNetworkAction.Submit && command.Action != DuelNetworkAction.Continue && command.Action != DuelNetworkAction.FinishRerolls && command.Action != DuelNetworkAction.Rematch)) return;
            lastSequence[player] = command.Sequence;
            CommandReceived?.Invoke(player, command);
        }

        public void PublishSnapshot(string hostJson, string guestJson)
        {
            if (!IsHost || !IsReady || interrupted) return;
            if (guest.HasValue) Send(Snapshot, guest.Value, guestJson);
            SnapshotReceived?.Invoke(hostJson);
        }

        private void OnSnapshot(ulong sender, FastBufferReader reader)
        {
            if (IsHost || interrupted || sender != NetworkManager.ServerClientId) return;
            try { SnapshotReceived?.Invoke(Read(reader)); }
            catch (Exception ex) when (ex is ArgumentException || ex is OverflowException || ex is InvalidCastException)
            { StopForDisconnect("対戦データを受信できませんでした。"); }
        }

        private static string Read(FastBufferReader reader)
        {
            if (reader.Length > MaxPayload) throw new OverflowException();
            reader.ReadValueSafe(out string value);
            return value;
        }

        private void Send(string name, ulong target, string value)
        {
            value = value ?? string.Empty;
            int size = sizeof(int) + value.Length * sizeof(char);
            if (size > MaxPayload) throw new ArgumentException("対戦データが大きすぎます。");
            using (var writer = new FastBufferWriter(size, Allocator.Temp))
            {
                writer.WriteValueSafe(value);
                manager.CustomMessagingManager.SendNamedMessage(name, target, writer, NetworkDelivery.ReliableFragmentedSequenced);
            }
        }

        private void OnDisconnected(ulong client)
        {
            if (leaving || interrupted) return;
            if (!IsHost || (guest.HasValue && client == guest.Value)) StopForDisconnect("相手との接続が切れたため対戦を中断しました。");
        }

        private void StopForDisconnect(string reason)
        {
            interrupted = true;
            IsReady = false;
            Interrupted?.Invoke(reason);
        }

        private void OnTransportFailure() => StopForDisconnect("通信エラーにより対戦を中断しました。");

        public async Task LeaveAsync()
        {
            leaving = true;
            IsReady = false;
            var oldSession = session;
            session = null;
            try { if (oldSession != null) await oldSession.LeaveAsync(); }
            finally
            {
                if (manager != null)
                {
                    manager.OnClientDisconnectCallback -= OnDisconnected;
                    manager.OnTransportFailure -= OnTransportFailure;
                    manager.Shutdown();
                    Destroy(manager.gameObject);
                    manager = null;
                }
                handlersRegistered = false;
                guest = null;
            }
        }

        private async void OnDestroy()
        {
            try { await LeaveAsync(); }
            catch (Exception ex) { Debug.LogWarning("対戦セッションの終了に失敗しました: " + ex.GetType().Name); }
        }
    }
}
