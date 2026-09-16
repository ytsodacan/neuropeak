using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace NeuroPeak.Core
{
    public sealed class MainThreadCommandQueue : MonoBehaviour
    {
        private const int MaxCommandsPerFrame = 32;

        private static MainThreadCommandQueue? _instance;

        private readonly ConcurrentQueue<Action> _pending = new ConcurrentQueue<Action>();

        public static MainThreadCommandQueue? Instance => _instance;

        public static void Enqueue(Action command)
        {
            MainThreadCommandQueue? queue = _instance;
            if (queue == null)
            {
                NeuroPeakPlugin.Log?.LogWarning("Dropped a command because the main thread queue is not running");
                return;
            }

            queue._pending.Enqueue(command);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }

            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            for (int drained = 0; drained < MaxCommandsPerFrame; drained++)
            {
                if (!_pending.TryDequeue(out Action command)) return;

                try
                {
                    command();
                }
                catch (Exception e)
                {
                    NeuroPeakPlugin.Log?.LogError($"Queued command threw: {e}");
                }
            }
        }
    }
}
