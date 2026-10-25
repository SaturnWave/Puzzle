#nullable enable
using System;
using System.Collections.Generic;
using Puzzle.Client.Words;

namespace Puzzle.Client.Flow
{
    /// <summary>
    /// One part of the credits card: a heading in the language the game speaks, over lines that stay in English as
    /// their licences give them. The licence's own text (the MIT notice) is a part without a heading: its first line
    /// names it.
    /// </summary>
    public sealed class CreditSection
    {
        /// <param name="heading">The heading in the language the game speaks; nothing for a licence's own text.</param>
        /// <param name="lines">The lines, in English, each a paragraph of its own.</param>
        /// <exception cref="ArgumentException">A part with no line.</exception>
        public CreditSection(string? heading, IReadOnlyList<string> lines)
        {
            if (lines.Count == 0)
            {
                throw new ArgumentException("A part of the credits has a line at least.", nameof(lines));
            }

            Heading = heading;
            Lines = lines;
        }

        /// <summary>The heading; nothing for a licence's own text, which needs none.</summary>
        public string? Heading { get; }

        public IReadOnlyList<string> Lines { get; }
    }

    /// <summary>
    /// What the credits card says (the settings card's row "Künye"): who made the pictures, typefaces and sounds the
    /// game uses, and under which licences. Its nameplate, its first line and the headings are said in the language
    /// the game speaks (<see cref="Speech"/>); the lines under the headings are licence notices and stay in English in
    /// every language, word for word as the licence records have them (art/avatar/licences.json, art/fonts/licences.json,
    /// art/paintings/room-NN/licences.json, art/sounds/licences.json, and unity/Packages/licences.json for the software
    /// the game carries; the tests hold these lines to them). A notice is
    /// no word of a table: a translator does not change it. Every line is written in the game's letters (a "©" of a
    /// record is written "(c)"). No Unity dependency.
    /// </summary>
    /// <remarks>
    /// The painter's head is the Blender Foundation's Einar (CC BY 4.0), which asks for its credit line and the word
    /// "modified"; his body is Microsoft Rocketbox (MIT), whose notice must go with every copy, so it is shown whole.
    /// The paintings, the sounds and the textures of the painter's clothes ask for no credit (public domain, CC0): they
    /// are named all the same. The typefaces' copyright lines go with the SIL Open Font License they are shared under.
    /// The software the game carries from its packages is named with its licences: the Aptabase SDK and Json.NET are
    /// MIT, whose notice must go with every copy, so their copyright lines are shown with the MIT text whole; the parts
    /// of Unity In-App Purchasing that its notices list under the Apache License 2.0 are named with their copyright
    /// lines and the licence's address. The music is the owner's own and is not listed.
    /// </remarks>
    public static class CreditWords
    {
        /// <summary>The painter's head: the Einar rig's credit line, as its CC BY licence asks for it, with "modified".</summary>
        public const string EinarLine = "Einar Rig (CC-BY) Blender Foundation | studio.blender.org, CC BY 4.0 (https://creativecommons.org/licenses/by/4.0/), modified.";

        /// <summary>The painter's body, clothes and hands.</summary>
        public const string RocketboxLine = "Microsoft Rocketbox, Business_Male_04 (https://github.com/microsoft/Microsoft-Rocketbox), MIT License, modified: head removed, shirt dyed ochre, arms and hands re-posed.";

        /// <summary>The textures of the painter's beret, apron and palette, and the light he was rendered in.</summary>
        public const string PolyHavenLine = "Textures and studio light: Poly Haven (polyhaven.com), CC0.";

        /// <summary>The MIT License of Microsoft Rocketbox, whole, a paragraph a line.</summary>
        public static IReadOnlyList<string> RocketboxLicence { get; } = new[]
        {
            "MIT License",
            "Copyright (c) 2020 Microsoft",
            "Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the \"Software\"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:",
            "The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.",
            "THE SOFTWARE IS PROVIDED \"AS IS\", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.",
        };

        /// <summary>The museum's paintings: public-domain works, in photographs their museums give for any use.</summary>
        public const string PaintingsLine =
            "Public-domain works, in photographs released for any use by the museums that own them: National Gallery of Art (CC0), "
            + "The Metropolitan Museum of Art (CC0), Rijksmuseum (Public Domain Mark), Art Institute of Chicago (CC0), "
            + "Statens Museum for Kunst (Public Domain Mark), Nationalmuseum (Public Domain Mark), Cleveland Museum of Art (CC0), "
            + "Skokloster Castle (Public Domain Mark), Finnish National Gallery (CC0), J. Paul Getty Museum (CC0). "
            + "One photograph is Google Arts & Culture's public-domain reproduction of a painting in the Art Institute of Chicago.";

        /// <summary>The typefaces' copyright lines, and the licence they are shared under.</summary>
        public static IReadOnlyList<string> TypefaceLines { get; } = new[]
        {
            "Gabarito: Copyright 2023 The Gabarito Project Authors (https://github.com/naipefoundry/gabarito)",
            "Paytone One: Copyright 2011 The Paytone Project Authors (https://github.com/googlefonts/paytoneFont), with Reserved Font Names \"Paytone\" and \"Paytone One\".",
            "Young Serif: Copyright 2023 The Young Serif Project Authors (https://github.com/noirblancrouge/YoungSerif)",
            "Noto Sans, Noto Serif: Copyright 2022 The Noto Project Authors (https://github.com/notofonts/latin-greek-cyrillic)",
            "Noto Sans KR: Copyright 2014-2021 Adobe (http://www.adobe.com/), with Reserved Font Name 'Source'",
            "Noto Serif KR: Copyright 2012 Google Inc. All Rights Reserved.",
            "Noto Sans JP, Noto Sans SC, Noto Sans TC: (c) 2014-2021 Adobe (http://www.adobe.com/).",
            "Noto Serif JP, Noto Serif SC, Noto Serif TC: (c) 2017-2024 Adobe (http://www.adobe.com/).",
            "All under the SIL Open Font License, Version 1.1.",
        };

        /// <summary>The sounds of play.</summary>
        public const string SoundsLine = "Sounds of play: Kenney (www.kenney.nl), Creative Commons CC0 1.0 Universal.";

        /// <summary>The software the game carries from its packages, by package, with its licence.</summary>
        public static IReadOnlyList<string> SoftwareLines { get; } = new[]
        {
            "Aptabase SDK for Unity 0.3.1 (https://github.com/aptabase/aptabase-unity), MIT License.",
            "Json.NET (Newtonsoft.Json, https://www.newtonsoft.com/json), in Unity's package com.unity.nuget.newtonsoft-json 3.2.2, MIT License.",
            "In Unity In-App Purchasing 5.4.4, as its notices list them: Google Play Billing Library 9.0.0, Copyright (c) 2023 Google, LLC; Apple StoreKit 2, Copyright (c) 2023 Apple Computer, Inc.; under the Apache License 2.0 (https://www.apache.org/licenses/LICENSE-2.0).",
        };

        /// <summary>The MIT License of the software above, whole: its copyright lines, then its text, a paragraph a line.</summary>
        public static IReadOnlyList<string> SoftwareLicence { get; } = new[]
        {
            "MIT License",
            "Copyright (c) 2023 Aptabase",
            "Copyright (c) 2007 James Newton-King",
            "Copyright (c) 2016 SaladLab",
            "Copyright (c) 2019 Kalle Jillheden (jilleJr)",
            "Copyright (c) 2019 Mike Wuetherick",
            "Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the \"Software\"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:",
            "The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.",
            "THE SOFTWARE IS PROVIDED \"AS IS\", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.",
        };

        /// <summary>The credits card's nameplate, and the settings card's row that opens it.</summary>
        public static string Title => Speech.Say(WordId.SettingsCredits);

        /// <summary>The card's first line: the lines under the headings stay in English.</summary>
        public static string InEnglish => Speech.Say(WordId.CreditsInEnglish);

        /// <summary>The card's parts, in the order it shows them: the painter and his licence's text, the paintings, the typefaces, the sounds, the software and its licence's text.</summary>
        public static IReadOnlyList<CreditSection> Sections => new[]
        {
            new CreditSection(Speech.Say(WordId.CreditsPainter), new[] { EinarLine, RocketboxLine, PolyHavenLine }),
            new CreditSection(heading: null, RocketboxLicence),
            new CreditSection(Speech.Say(WordId.CreditsPaintings), new[] { PaintingsLine }),
            new CreditSection(Speech.Say(WordId.CreditsTypefaces), TypefaceLines),
            new CreditSection(Speech.Say(WordId.CreditsSounds), new[] { SoundsLine }),
            new CreditSection(Speech.Say(WordId.CreditsSoftware), SoftwareLines),
            new CreditSection(heading: null, SoftwareLicence),
        };
    }
}
