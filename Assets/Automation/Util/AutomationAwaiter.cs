using System;
using System.Collections;
using Network.Messages;
using UnityEngine;

namespace Automation.Util {
    public sealed class AutomationResult {
        public bool Succeeded { get; private set; }
        public string ErrorMessage { get; private set; }
        public NetworkErrorMessage NetworkError { get; private set; }

        public static AutomationResult Ok() {
            return new AutomationResult { Succeeded = true };
        }

        public static AutomationResult Fail(string message, NetworkErrorMessage networkError = null) {
            return new AutomationResult {
                Succeeded = false,
                ErrorMessage = message,
                NetworkError = networkError
            };
        }

        public void ThrowIfFailed() {
            if (Succeeded)
                return;
            throw new AutomationException(ErrorMessage ?? "Automation operation failed", NetworkError);
        }
    }

    public class AutomationException : Exception {
        public NetworkErrorMessage NetworkError { get; }

        public AutomationException(string message, NetworkErrorMessage networkError = null) : base(message) {
            NetworkError = networkError;
        }
    }

    public sealed class SignalAwaiter {
        bool m_signaled;

        public bool IsSignaled => m_signaled;

        public void Signal() {
            m_signaled = true;
        }

        public void Reset() {
            m_signaled = false;
        }
    }

    public static class AutomationAwaiter {
        public static IEnumerator WaitForSignal(SignalAwaiter awaiter, float timeoutSeconds, string timeoutMessage) {
            if (awaiter == null)
                throw new ArgumentNullException(nameof(awaiter));

            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!awaiter.IsSignaled) {
                if (Time.realtimeSinceStartup >= deadline)
                    throw new AutomationException(timeoutMessage ?? "Timed out waiting for signal");
                yield return null;
            }
        }

        public static IEnumerator WaitUntil(Func<bool> predicate, float timeoutSeconds, string timeoutMessage) {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!predicate()) {
                if (Time.realtimeSinceStartup >= deadline)
                    throw new AutomationException(timeoutMessage ?? "Timed out waiting for condition");
                yield return null;
            }
        }

        public static IEnumerator WaitForNextFrame() {
            yield return null;
        }

        public static IEnumerator DelaySeconds(float seconds) {
            if (seconds <= 0f) {
                yield return null;
                yield break;
            }

            float deadline = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < deadline)
                yield return null;
        }
    }
}
