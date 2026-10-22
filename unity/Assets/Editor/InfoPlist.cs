#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Puzzle.Client.EditorTools
{
    /// <summary>What the app's Info.plist is to say of one key. A closed set: a key that says yes or no, or a key withdrawn.</summary>
    public abstract class PlistLine
    {
        private PlistLine(string key)
        {
            Key = string.IsNullOrWhiteSpace(key) ? throw new ArgumentException("A plist key has a name.", nameof(key)) : key;
        }

        /// <summary>The key's name, "ITSAppUsesNonExemptEncryption" for one.</summary>
        public string Key { get; }

        public static PlistLine Says(string key, bool value) => new Flag(key, value);

        public static PlistLine Withdraw(string key) => new Withdrawn(key);

        /// <summary>The key says yes or no.</summary>
        public sealed class Flag : PlistLine
        {
            internal Flag(string key, bool value)
                : base(key)
            {
                Value = value;
            }

            public bool Value { get; }

            /// <summary>The plist's element for the value: &lt;true/&gt; or &lt;false/&gt;.</summary>
            internal string ValueElement => Value ? "true" : "false";

            public override string ToString() => $"{Key} {ValueElement}";
        }

        /// <summary>The key is not in the plist, also where Unity wrote it.</summary>
        public sealed class Withdrawn : PlistLine
        {
            internal Withdrawn(string key)
                : base(key)
            {
            }

            public override string ToString() => $"Withdrawn {Key}";
        }
    }

    /// <summary>
    /// Writes the build's lines into an iOS app's Info.plist (the one Unity writes at the top of the Xcode project,
    /// which IosBuild's plist step hands over), an XML property list whose top is one dictionary: a key that says yes
    /// or no is set to it once, where it stands or else at the dictionary's end, on a line of its own; a withdrawn key
    /// goes, with its value. Nothing else of the list changes, its layout and line breaks included, but for how an
    /// empty element is spelled (&lt;true/&gt; is written &lt;true /&gt;, the same XML). The list's document type is kept as
    /// it is written and never fetched. This file has no Unity dependency (nor on Unity's iOS module), so the
    /// repository's tests compile it too.
    /// </summary>
    public static class InfoPlist
    {
        private const string Key = "key";

        /// <summary>The property list with every line written into it, in the list's order.</summary>
        /// <exception cref="XmlException">The text is not XML.</exception>
        /// <exception cref="InvalidOperationException">The XML is not a property list with a dictionary at its top, or a key in it has no value.</exception>
        public static string Apply(string plist, IEnumerable<PlistLine> lines)
        {
            XDocument document = Read(plist);
            XElement dictionary = document.Root is XElement top && top.Name.LocalName == "plist" && top.Element("dict") is XElement dict
                ? dict
                : throw new InvalidOperationException("The file is not a property list: its root is not <plist> with a <dict> in it.");
            foreach (PlistLine line in lines)
            {
                Write(dictionary, line);
            }

            return Text(document);
        }

        /// <summary>The document as it was read but for what was written into it: its line breaks too, on any system.</summary>
        private static string Text(XDocument document)
        {
            var settings = new XmlWriterSettings { OmitXmlDeclaration = true, NewLineHandling = NewLineHandling.None };
            var text = new StringBuilder();
            using (XmlWriter writer = XmlWriter.Create(text, settings))
            {
                document.Save(writer);
            }

            return (document.Declaration?.ToString() ?? string.Empty) + text;
        }

        private static XDocument Read(string plist)
        {
            // Apple's document type is named, not needed: with no resolver it is kept as text and never fetched.
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse, XmlResolver = null };
            using var reader = XmlReader.Create(new StringReader(plist), settings);
            XDocument document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);

            // The reader gives a document type without declarations of its own an empty one, which a writer prints as "[]".
            if (document.DocumentType is XDocumentType type && string.IsNullOrEmpty(type.InternalSubset))
            {
                type.InternalSubset = null;
            }

            return document;
        }

        private static void Write(XElement dictionary, PlistLine line)
        {
            XElement[] keys = dictionary.Elements(Key).Where(key => key.Value == line.Key).ToArray();
            switch (line)
            {
                case PlistLine.Flag flag:
                    var value = new XElement(flag.ValueElement);
                    if (keys.Length == 0)
                    {
                        Append(dictionary, new XElement(Key, flag.Key), value);
                        return;
                    }

                    ValueOf(keys[0]).ReplaceWith(value);
                    foreach (XElement again in keys.Skip(1))
                    {
                        RemovePair(again);
                    }

                    break;
                case PlistLine.Withdrawn _:
                    foreach (XElement key in keys)
                    {
                        RemovePair(key);
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(line), line, "A plist line the Info.plist step does not know.");
            }
        }

        /// <summary>A key's value: the element after it.</summary>
        private static XElement ValueOf(XElement key) =>
            key.ElementsAfterSelf().FirstOrDefault() ?? throw new InvalidOperationException($"The property list's key {key.Value} has no value.");

        private static void RemovePair(XElement key)
        {
            RemoveWithItsLine(ValueOf(key));
            RemoveWithItsLine(key);
        }

        /// <summary>An element goes with the line break and indentation before it, so no empty line is left.</summary>
        private static void RemoveWithItsLine(XElement element)
        {
            if (element.PreviousNode is XText before && string.IsNullOrWhiteSpace(before.Value))
            {
                before.Remove();
            }

            element.Remove();
        }

        /// <summary>A key and its value at the dictionary's end, each on a line of its own, indented as the keys before it.</summary>
        private static void Append(XElement dictionary, XElement key, XElement value)
        {
            string indentation = dictionary.Elements().FirstOrDefault()?.PreviousNode is XText before && string.IsNullOrWhiteSpace(before.Value)
                ? before.Value
                : "\n";
            object[] pair = { new XText(indentation), key, new XText(indentation), value };
            if (dictionary.LastNode is XText closing && string.IsNullOrWhiteSpace(closing.Value))
            {
                closing.AddBeforeSelf(pair);
            }
            else
            {
                dictionary.Add(pair);
            }
        }
    }
}
