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
            SprintAction.ActionName,
            InteractAction.ActionName,
            UseItemAction.ActionName,
            SelectSlotAction.ActionName,
            DropItemAction.ActionName
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
                new SprintAction(),
                new InteractAction(),
                new UseItemAction(),
                new SelectSlotAction(),
                new DropItemAction());
        }

        private static string ControlBriefing()
        {
            return
                "## You are climbing PEAK\n" +
                "You and your friends are climbing a mountain, from the beach at the bottom up to the summit. " +
                "It is a long way up and you cannot do it without resting, eating, and using what you find on the way.\n\n" +
                "**Getting around.** `move` walks you in a direction relative to where you are facing. `look` turns your " +
                "head, and you need it constantly, because almost everything else depends on what is in front of you. " +
                "`jump` only works with your feet on the ground.\n\n" +
                "**Climbing.** Face a wall and `grab` to latch on, then `move` to pull yourself along it. `sprint` while " +
                "holding on gives you a hard push upwards. `release` lets go, and if you are high up that means falling. " +
                "You need empty hands to climb, so put your item away first.\n\n" +
                "**Stuff.** `interact` picks things up, opens chests and lights campfires. `select_slot` takes something " +
                "out of your bag, `use_item` eats or uses it, `drop_item` throws it away.\n\n" +
                "**Staying alive.** Holding onto a wall burns stamina fast, and when it runs out you fall. Stamina comes " +
                "back on solid ground. Hunger, cold and injuries all shrink how much stamina you can have at all, so eat " +
                "when you find food. Campfires are checkpoints, so lighting one matters.\n\n" +
                "I will tell you what is around you as you go.";
        }
    }
}
