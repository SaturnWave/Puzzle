#nullable enable
using System;
using System.Linq;
using System.Xml.Linq;

namespace Puzzle.Client.EditorTools
{
    /// <summary>
    /// Keeps Google's logging transport from sending anything out of the game. Google Play's billing library (9.0.0,
    /// which Unity In-App Purchasing 5.4.4 brings) logs how it is used, under the log source PLAY_BILLING_LIBRARY,
    /// through Google's data transport (com.google.android.datatransport: transport-runtime and transport-backend-cct
    /// 3.1.8). The transport keeps those logs in a database of the game's own (databases/com.google.android.datatransport.events)
    /// and uploads them from the game's process to firebaselogging.googleapis.com, on a job of Android's job scheduler,
    /// as often as the server lets it: about every 16 minutes on the phone run of 26 October 2026 (T186), with the
    /// statistics declined and nothing bought, while the store was asked again and again for its products. The upload
    /// carries the phone's model, maker, system build, country, language, time zone and network type with the logs.
    /// <para>
    /// The transport learns where to send through one registration in the merged manifest: the meta-data
    /// <see cref="CctBackend"/> under its discovery service (<see cref="DiscoveryService"/>), which
    /// transport-backend-cct declares. The manifest step marks that registration for removal in the Unity library's
    /// manifest (tools:node="remove"), so Android's manifest merger takes it out of the library's declaration, as it
    /// takes out the permissions the build withdraws (<see cref="ManifestPermissions"/>). With no backend registered the
    /// transport sends nothing: an event is neither stored nor scheduled ("Transport backend 'cct' is not registered",
    /// handed to a callback the transport's own send ignores), and an event an earlier build stored is dropped unsent
    /// when its job runs ("Unknown backend …, deleting event batch"). The billing library's logger catches every failure
    /// of the transport and goes on ("logging failed."); purchases themselves go to Google Play's own app over
    /// Android's binder (com.android.vending.billing.InAppBillingService.BIND), never through the transport. The
    /// scheduler's service and receiver stay declared: an upload job an earlier build left behind finds them, and runs
    /// only to drop what it held.
    /// </para>
    /// The built package is read back too (<see cref="RequireWithdrawnFrom"/>): a registration that came through stops
    /// the build. This file has no Unity dependency, so the repository's tests compile it too.
    /// </summary>
    public static class ManifestLoggingBackend
    {
        /// <summary>The transport's service whose meta-data registers the backends it may send to.</summary>
        public const string DiscoveryService = "com.google.android.datatransport.runtime.backends.TransportBackendDiscovery";

        /// <summary>The registration of the backend that sends to Google's logging servers (its value is "cct").</summary>
        public const string CctBackend = "backend:com.google.android.datatransport.cct.CctBackendFactory";

        private static readonly XNamespace Android = "http://schemas.android.com/apk/res/android";
        private static readonly XNamespace Tools = "http://schemas.android.com/tools";

        /// <summary>
        /// The manifest with the backend's registration marked for removal: under the discovery service (declared for
        /// it, without attributes of its own, if the manifest has none), a meta-data named <see cref="CctBackend"/> with
        /// tools:node="remove" and nothing else. A manifest that has it so already comes back unchanged.
        /// </summary>
        /// <exception cref="System.Xml.XmlException">The text is not XML.</exception>
        /// <exception cref="InvalidOperationException">The XML is not an Android manifest with an application.</exception>
        public static string Withdraw(string manifest)
        {
            XDocument document = XDocument.Parse(manifest, LoadOptions.PreserveWhitespace);
            XElement root = document.Root is XElement top && top.Name.LocalName == "manifest"
                ? top
                : throw new InvalidOperationException("The file is not an Android manifest: its root is not <manifest>.");
            XElement application = root.Element("application")
                ?? throw new InvalidOperationException("The manifest has no <application> to withdraw the logging transport's backend under.");
            if (root.Attribute(XNamespace.Xmlns + "tools") == null)
            {
                root.Add(new XAttribute(XNamespace.Xmlns + "tools", Tools.NamespaceName));
            }

            XElement service = application.Elements("service").FirstOrDefault(element => NameOf(element) == DiscoveryService)
                ?? AddService(application);
            service.Elements("meta-data").Where(element => NameOf(element) == CctBackend).Remove();
            service.Add(new XElement("meta-data", new XAttribute(Android + "name", CctBackend), new XAttribute(Tools + "node", "remove")));

            // The line break after the declaration is the document's own whitespace, kept as it was read.
            return (document.Declaration?.ToString() ?? string.Empty) + document.ToString(SaveOptions.DisableFormatting);
        }

        /// <summary>Stops a package whose manifest still registers the backend: the transport would send again.</summary>
        /// <param name="dump">What Android's own tool says of the package's manifest: <c>aapt dump xmltree puzzle.apk AndroidManifest.xml</c>.</param>
        /// <exception cref="InvalidOperationException">The text is not such a dump, or the package registers the backend.</exception>
        public static void RequireWithdrawnFrom(string dump)
        {
            if (dump == null || !dump.Contains("E: manifest"))
            {
                throw new InvalidOperationException("The text is not what aapt says of a package's manifest: it has no manifest element.");
            }

            if (dump.Contains($"\"{CctBackend}\""))
            {
                throw new InvalidOperationException(
                    $"The package's merged manifest registers {CctBackend}: Google's logging transport would upload the billing library's logs from the phone. "
                    + "The manifest step is to mark it for removal in the Unity library's manifest.");
            }
        }

        private static XElement AddService(XElement application)
        {
            var service = new XElement("service", new XAttribute(Android + "name", DiscoveryService));
            application.Add(service);
            return service;
        }

        private static string? NameOf(XElement element) => (string?)element.Attribute(Android + "name");
    }
}
