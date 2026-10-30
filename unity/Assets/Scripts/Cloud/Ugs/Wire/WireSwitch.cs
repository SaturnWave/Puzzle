#nullable enable
using System;
using System.Reflection;
using Unity.Services.Core.Internal;
using Unity.Services.Wire.Internal;

namespace Puzzle.Client.Cloud
{
    /// <summary>
    /// Keeps Unity's Wire from opening its websocket (2026-10-27, T186). Wire (com.unity.services.wire 1.6.0, which Cloud
    /// Code 2.10.4 depends on) connects to wss://wire.unity3d.com at a sign-in and holds the connection open while the
    /// game is in front, for Cloud Code's subscriptions to player and project messages. The game subscribes to none: it
    /// calls its module's endpoints over HTTPS, and Cloud Save, Remote Config and Authentication do not use Wire. On the
    /// phone run of 27 October the socket stayed open in about half the starts, trading a few hundred bytes a minute and
    /// keeping the radio awake, for nothing.
    /// <para>
    /// Wire has no public switch, and the services start it with themselves. Its client's own Disable, which disconnects
    /// and keeps the connection from being made again, is called once the services have started and before the player
    /// signs in, so the socket is never opened. The client is the services' registered IWire (an internal component of
    /// Unity's services, which this assembly references for it alone), and its class is internal to Wire, so its public
    /// Disable is found by reflection (Wire 1.6.0, pinned in packages-lock.json; kept from stripping by Assets/link.xml).
    /// Where it is not found, Wire stays as it was and the log says so; nothing else changes.
    /// </para>
    /// </summary>
    public static class WireSwitch
    {
        private const string DisableName = "Disable";

        /// <summary>Turns Wire off for the run; called once Unity's services have started, before the sign-in.</summary>
        /// <param name="log">Where it is said whether Wire was turned off: the device's log.</param>
        public static void TurnOff(Action<string> log)
        {
            if (!(CoreRegistry.Instance is CoreRegistry registry) || !registry.TryGetServiceComponent(out IWire wire))
            {
                log("Unity's Wire is not among the services started: there is no websocket to keep closed.");
                return;
            }

            MethodInfo? disable = wire.GetType().GetMethod(DisableName, BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
            if (disable == null)
            {
                log($"Unity's Wire ({wire.GetType().FullName}) has no {DisableName} in this version: its websocket opens at the sign-in as Wire decides.");
                return;
            }

            try
            {
                disable.Invoke(wire, null);
                log("Unity's Wire is turned off for the run: no websocket to wire.unity3d.com is opened (the game subscribes to no messages).");
            }
            catch (TargetInvocationException failure) when (failure.InnerException != null)
            {
                log($"Unity's Wire could not be turned off, so its websocket opens at the sign-in as Wire decides: {failure.InnerException.Message}");
            }
        }
    }
}
