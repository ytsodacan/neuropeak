using NeuroPeak.Core;
using NeuroSdk.Actions;
using NeuroSdk.Messages.Outgoing;
using NeuroSdk.Websocket;
using UnityEngine;
using UnityEngine.Events;

namespace NeuroPeak.Actions
{
    public sealed class NeuroPeakActionRegistry : MonoBehaviour
    {
        private static readonly string[] ActionNames =
        {
            MoveAction.ActionName,
            LookAction.ActionName,
            JumpAction.ActionName,
            GrabAction.ActionName,
            ReleaseAction.ActionName,
            SprintAction.ActionName
        };

        private bool _registered;
        private bool _briefed;
        private bool _applicationQuitting;
        private bool _connectHookInstalled;
        private bool _sawFirstConnection;

        private void Update()
        {
            WebsocketConnection? connection = WebsocketConnection.Instance;
            if (connection == null) return;

            InstallConnectHook(connection);

            if (!_registered)
            {
                RegisterActions();
                _registered = true;
            }

            if (!_briefed && PeakStateTracker.Current.InGame)
            {
                NeuroContext.SendAmbient(ControlBriefing());
                _briefed = true;
            }
        }

        private void OnApplicationQuit() => _applicationQuitting = true;

        private void OnDestroy()
        {
            if (_applicationQuitting) return;
            if (!_registered) return;
            if (WebsocketConnection.Instance == null) return;

            NeuroActionHandler.UnregisterActions(ActionNames);
            _registered = false;
        }

        private void InstallConnectHook(WebsocketConnection connection)
        {
            if (_connectHookInstalled) return;

            connection.onConnected ??= new UnityEvent();
            connection.onConnected.AddListener(OnConnected);
            _connectHookInstalled = true;
        }

        private void OnConnected()
        {
            if (!_sawFirstConnection)
            {
                _sawFirstConnection = true;
                return;
            }

            WebsocketConnection? connection = WebsocketConnection.Instance;
            if (connection == null) return;

            connection.Send(new Startup());
            NeuroActionHandler.ResendRegisteredActions();
            _briefed = false;
        }

        private static void RegisterActions()
        {
            NeuroActionHandler.RegisterActions(
                new MoveAction(),
                new LookAction(),
                new JumpAction(),
                new GrabAction(),
                new ReleaseAction(),
                new SprintAction());
        }

        private static string ControlBriefing()
        {
            return
                "## You are climbing in PEAK\n" +
                "You and your friends have to climb a mountain together, from the beach at the bottom to the peak at the top.\n" +
                "- `move` walks you in a direction relative to where you are facing, for up to two seconds at a time.\n" +
                "- `look` turns your head. Face a wall before you try to grab it.\n" +
                "- `jump` only works with your feet on the ground.\n" +
                "- `grab` latches onto rock, roots, ropes and vines you are looking at, within arm's reach. Holding on drains stamina.\n" +
                "- `sprint` runs on the ground, and pulls you upwards while you are holding a wall.\n" +
                "- `release` lets go. If you let go high up, you fall and take damage.\n" +
                "Stamina refills when you stand on solid ground. If it runs out while you are holding a wall, you drop.\n" +
                "I will describe what is around you as you climb.";
        }
    }
}
