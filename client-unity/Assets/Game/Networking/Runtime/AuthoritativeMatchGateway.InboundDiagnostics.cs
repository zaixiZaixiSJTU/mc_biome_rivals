#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Threading;

namespace BiomeRivals.Networking
{
    public sealed partial class AuthoritativeMatchGateway
    {
        private readonly object _inboundDeliveryLock = new object();
        private readonly Queue<HeldInboundMessage> _heldInbound = new Queue<HeldInboundMessage>();
        private bool _holdingInbound, _drainingInbound;
        private int _inboundHoldGeneration, _heldInboundCharacters;

        public int HeldIncomingMessageCount { get { lock (_inboundDeliveryLock) return _heldInbound.Count; } }

        public IDisposable HoldIncomingMatchMessages()
        {
            lock (_inboundDeliveryLock)
            {
                if (_disposed || _compatibilityFailed || !_currentStatus.CanSendCommands)
                    throw new InvalidOperationException("Inbound diagnostics require a compatible ready match.");
                if (_holdingInbound || _drainingInbound)
                    throw new InvalidOperationException("An inbound hold is already active or draining.");
                _holdingInbound = true;
                return new InboundHoldScope(this, ++_inboundHoldGeneration);
            }
        }

        private void ReleaseInboundDeliveryHold(int generation)
        {
            lock (_inboundDeliveryLock)
            {
                if (!_holdingInbound || generation != _inboundHoldGeneration) return;
                _holdingInbound = false;
                _drainingInbound = true;
                try
                {
                    // Keep the fence through the drain. Reentrant arrivals append behind existing frames;
                    // concurrent socket arrivals cannot overtake the private snapshot/event sequence.
                    while (!_disposed && !_compatibilityFailed && generation == _inboundHoldGeneration && _heldInbound.Count > 0)
                    {
                        var message = _heldInbound.Dequeue();
                        _heldInboundCharacters -= message.Json?.Length ?? 0;
                        DeliverMessage(message.Opcode, message.Json);
                    }
                }
                finally
                {
                    // A fault callback may have acquired a new scope. Never cancel that newer generation.
                    if (generation == _inboundHoldGeneration) CancelInboundDeliveryHold();
                }
            }
        }

        private void CancelInboundDeliveryHold()
        {
            lock (_inboundDeliveryLock)
            {
                _holdingInbound = false;
                _drainingInbound = false;
                _inboundHoldGeneration++;
                _heldInbound.Clear();
                _heldInboundCharacters = 0;
            }
        }

        private sealed class HeldInboundMessage
        {
            public readonly int Opcode;
            public readonly string Json;
            public HeldInboundMessage(int opcode, string json) { Opcode = opcode; Json = json; }
        }

        private sealed class InboundHoldScope : IDisposable
        {
            private AuthoritativeMatchGateway _gateway;
            private readonly int _generation;
            public InboundHoldScope(AuthoritativeMatchGateway gateway, int generation) { _gateway = gateway; _generation = generation; }
            public void Dispose() => Interlocked.Exchange(ref _gateway, null)?.ReleaseInboundDeliveryHold(_generation);
        }
    }
}
#endif
