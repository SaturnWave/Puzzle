#nullable enable
using System;
using System.Reflection;
using System.Threading;

namespace Puzzle.Client.Cloud
{
    /// <summary>
    /// The work that waits for Unity's main thread, run at once on the way out to the background (2026-10-27, T530).
    /// Unity stops its player loop as soon as Android pauses the game, and what Unity's services queue for the main
    /// thread runs only when the game comes back: Cloud Save 3.4.2 makes and sends its UnityWebRequest in work it hands
    /// to Unity's synchronization context, so the upload the way out asked for did not even open a connection on the
    /// phone run of 27 October (the game was cut off the network five seconds later, and the upload went a minute after,
    /// when the game was opened again). Run here, in the pause's own call, the request is handed to Android's network,
    /// which carries it in the seconds Android leaves a paused game; its answer is taken in when the game is in front
    /// again, and a game closed meanwhile finds its write in the save at the next start (Cloud/CloudSync's unanswered write).
    /// <para>
    /// Unity keeps the queue's runner internal: the synchronization context of the main thread is Unity's own, whose public
    /// <c>Exec</c> runs what waits, as Unity's loop does once a frame. It is found by reflection (Unity 6000.6.4f1, pinned;
    /// kept from stripping by Assets/link.xml), and where it is not found nothing is run, which the log says: the upload
    /// then goes when the game is in front again, as before. A few passes run what the work that ran queued after it.
    /// </para>
    /// This file has no Unity dependency of its own; it is Unity's context it runs.
    /// </summary>
    public static class MainThreadQueue
    {
        /// <summary>The runs of the queue: the cloud's upload needs one, the rest is for work that queues more on the way.</summary>
        private const int Passes = 3;

        private const string RunnerName = "Exec";

        /// <summary>Runs what waits for the main thread now; called on the main thread, in the pause's call.</summary>
        /// <param name="log">Where it is said that the queue could not be run.</param>
        /// <param name="fault">Where a fault of the work that ran goes: the device's log as an exception.</param>
        public static void RunWhatWaits(Action<string> log, Action<Exception> fault)
        {
            SynchronizationContext? context = SynchronizationContext.Current;
            MethodInfo? runner = context?.GetType().GetMethod(RunnerName, BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
            if (context == null || runner == null)
            {
                log($"The main thread's queue cannot be run on the way out ({context?.GetType().FullName ?? "no synchronization context"} has no {RunnerName}): the cloud's upload goes when the game is in front again.");
                return;
            }

            for (int pass = 0; pass < Passes; pass++)
            {
                try
                {
                    runner.Invoke(context, null);
                }
                catch (TargetInvocationException failure) when (failure.InnerException != null)
                {
                    // A queued piece of work threw, as it would in Unity's own loop; what is left of the queue runs in the next pass.
                    fault(failure.InnerException);
                }
            }
        }
    }
}
