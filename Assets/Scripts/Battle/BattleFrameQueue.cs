using System;
using System.Collections.Concurrent;
using Network.Messages;

namespace Battle {
    /// <summary>
    /// Thread-safe queue for <see cref="BattleFrameResponse"/>; drained in order on the main thread.
    /// </summary>
    public sealed class BattleFrameQueue {
        private readonly ConcurrentQueue<BattleFrameResponse> m_queue = new ConcurrentQueue<BattleFrameResponse>();

        public void Enqueue(BattleFrameResponse frame) {
            if (frame == null) {
                return;
            }

            m_queue.Enqueue(frame);
        }

        public void Clear() {
            while (m_queue.TryDequeue(out _)) { }
        }

        public void DrainAll(Action<BattleFrameResponse> handler) {
            if (handler == null) {
                return;
            }

            while (m_queue.TryDequeue(out var frame)) {
                handler(frame);
            }
        }
    }
}
