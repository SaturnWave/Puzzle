#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Puzzle.Client.EditorTools
{
    /// <summary>What the package's manifest is to say of one Android permission. A closed set.</summary>
    public enum PermissionWish
    {
        /// <summary>The package asks for it.</summary>
        Asked,

        /// <summary>The package does not ask for it, also where Unity or a library would: the merged manifest leaves it out.</summary>
        Withdrawn,
    }

    /// <summary>One line of the build's list of permissions: a permission by its Android name, and what the manifest says of it.</summary>
    public sealed class PermissionLine
    {
        private PermissionLine(string name, PermissionWish wish)
        {
            Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("A permission has a name.", nameof(name)) : name;
            Wish = wish;
        }

        /// <summary>The permission's Android name, "android.permission.VIBRATE" for one.</summary>
        public string Name { get; }

        public PermissionWish Wish { get; }

        public static PermissionLine Ask(string name) => new PermissionLine(name, PermissionWish.Asked);

        public static PermissionLine Withdraw(string name) => new PermissionLine(name, PermissionWish.Withdrawn);

        public override string ToString() => $"{Wish} {Name}";
    }

    /// <summary>
    /// Writes the build's permissions into an Android manifest (the Unity library's, which AndroidBuild's manifest step
    /// hands over): a permission asked for is declared once; one withdrawn loses its declaration and is marked for
    /// removal (tools:node="remove"), so that Android's manifest merger takes it out of what Unity and the libraries
    /// declare as well. Nothing else of the manifest changes. This file has no Unity dependency, so the repository's
    /// tests compile it too.
    /// </summary>
    public static class ManifestPermissions
    {
        private const string UsesPermission = "uses-permission";
        private static readonly XNamespace Android = "http://schemas.android.com/apk/res/android";
        private static readonly XNamespace Tools = "http://schemas.android.com/tools";

        /// <summary>The manifest with every line of the list written into it, in the list's order.</summary>
        /// <exception cref="System.Xml.XmlException">The text is not XML.</exception>
        /// <exception cref="InvalidOperationException">The XML is not an Android manifest.</exception>
        public static string Apply(string manifest, IEnumerable<PermissionLine> lines)
        {
            XDocument document = XDocument.Parse(manifest, LoadOptions.PreserveWhitespace);
            XElement root = document.Root is XElement top && top.Name.LocalName == "manifest"
                ? top
                : throw new InvalidOperationException("The file is not an Android manifest: its root is not <manifest>.");
            foreach (PermissionLine line in lines)
            {
                Write(root, line);
            }

            // The line break after the declaration is the document's own whitespace, kept as it was read.
            return (document.Declaration?.ToString() ?? string.Empty) + document.ToString(SaveOptions.DisableFormatting);
        }

        private static void Write(XElement root, PermissionLine line)
        {
            XElement[] declared = root.Elements(UsesPermission).Where(element => (string?)element.Attribute(Android + "name") == line.Name).ToArray();
            switch (line.Wish)
            {
                case PermissionWish.Asked:
                    if (declared.Length == 1 && declared[0].Attribute(Tools + "node") == null)
                    {
                        return; // asked for already, as it is to be
                    }

                    declared.Remove();
                    Declare(root, new XElement(UsesPermission, new XAttribute(Android + "name", line.Name)));
                    break;
                case PermissionWish.Withdrawn:
                    declared.Remove();
                    if (root.Attribute(XNamespace.Xmlns + "tools") == null)
                    {
                        root.Add(new XAttribute(XNamespace.Xmlns + "tools", Tools.NamespaceName));
                    }

                    Declare(root, new XElement(UsesPermission, new XAttribute(Android + "name", line.Name), new XAttribute(Tools + "node", "remove")));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(line), line.Wish, "A wish for a permission the manifest step does not know.");
            }
        }

        /// <summary>Puts a declaration where Android's own manifests have their permissions: before the application.</summary>
        private static void Declare(XElement root, XElement permission)
        {
            if (root.Element("application") is XElement application)
            {
                application.AddBeforeSelf(permission);
            }
            else
            {
                root.Add(permission);
            }
        }
    }
}
