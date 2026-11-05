using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Puzzle.Core.Tests;

/// <summary>
/// The game's one piece of music (#9) as the repository keeps it: the record art/sounds/music.json, which says where
/// the music came from, under what terms, and how tools/music/cut-loop.py cuts it; and the two clips that tool wrote
/// into the Unity project, which must be what the record says. The source itself is the owner's file and is not
/// read here: the tool checks it by its sha256 before it cuts (<c>py tools/music/cut-loop.py --check</c>).
/// </summary>
public class MusicRecordTests
{
    private const double FullScale = 32768.0;

    private static readonly JsonSerializerOptions AsWritten = new() { PropertyNameCaseInsensitive = true };

    private static readonly Lazy<MusicRecord> Record = new(() =>
        JsonSerializer.Deserialize<MusicRecord>(File.ReadAllText(RepoFiles.MusicRecordPath), AsWritten)
        ?? throw new InvalidDataException("art/sounds/music.json holds no record."));

    private static readonly Lazy<Wave> Intro = new(() => Wave.Read(ClipPath(Record.Value.Cut.IntroFile)));

    private static readonly Lazy<Wave> Loop = new(() => Wave.Read(ClipPath(Record.Value.Cut.LoopFile)));

    private static LoopRecord Chosen => Record.Value.Cut.Loops[Record.Value.Cut.Loop];

    [Fact]
    public void The_chosen_loop_is_one_of_the_three_the_owner_heard_and_each_leaves_room_for_its_crossfade()
    {
        CutRecord cut = Record.Value.Cut;
        long crossfadeFrames = (long)Math.Round(cut.CrossfadeSeconds * Record.Value.Source.SampleRate);

        Assert.Equal(new[] { "M1-A", "M1-B", "M1-C" }, cut.Loops.Keys.OrderBy(name => name, StringComparer.Ordinal));
        Assert.Equal((1542656, 5667689), (cut.Loops["M1-A"].StartSample, cut.Loops["M1-A"].EndSample));
        Assert.Equal((1611776, 6576096), (cut.Loops["M1-B"].StartSample, cut.Loops["M1-B"].EndSample));
        Assert.Equal((1712640, 6116681), (cut.Loops["M1-C"].StartSample, cut.Loops["M1-C"].EndSample));
        Assert.Contains(cut.Loop, cut.Loops.Keys);
        Assert.All(cut.Loops.Values, loop =>
        {
            Assert.True(loop.StartSample >= crossfadeFrames, $"a loop from {loop.StartSample} has no {crossfadeFrames} frames before it to blend in");
            Assert.True(loop.EndSample - loop.StartSample > crossfadeFrames && loop.EndSample <= Record.Value.Source.Frames);
        });
    }

    [Fact]
    public void The_record_says_whose_account_made_the_music_when_with_what_prompt_and_that_it_may_be_used_commercially()
    {
        LicenceRecord licence = Record.Value.Licence;
        string brief = File.ReadAllText(RepoFiles.MusicBriefPath);

        Assert.Equal("Suno", licence.MadeWith);
        Assert.Contains("Suno Premier", licence.Account);
        Assert.Equal("2026-10-20", licence.Generated);
        Assert.False(string.IsNullOrWhiteSpace(licence.CommercialUse));
        Assert.True(licence.Instrumental);
        Assert.Contains(licence.Prompt, brief);
        Assert.Matches(new Regex("^[0-9a-f]{64}$"), Record.Value.Source.Sha256);
        Assert.Equal("M1 Müze Teması.wav", Record.Value.Source.File);
    }

    [Fact]
    public void The_sounds_of_play_keep_their_CC0_record_and_the_music_is_not_among_them()
    {
        using JsonDocument sounds = JsonDocument.Parse(File.ReadAllText(RepoFiles.SoundLicencesPath));
        JsonElement[] listed = sounds.RootElement.GetProperty("sounds").EnumerateArray().ToArray();

        Assert.Equal(15, listed.Length);
        Assert.All(listed, sound => Assert.StartsWith("Creative Commons CC0", sound.GetProperty("licence").GetString()));
        Assert.DoesNotContain(listed, sound => sound.GetProperty("file").GetString()!.EndsWith(".wav", StringComparison.Ordinal));
    }

    [Fact]
    public void Both_clips_keep_the_sources_rate_channels_and_samples_and_last_as_long_as_the_cut()
    {
        SourceRecord source = Record.Value.Source;

        Assert.All(new[] { Intro.Value, Loop.Value }, clip => Assert.Equal((source.SampleRate, source.Channels, 16), (clip.Rate, clip.Channels, clip.Bits)));
        Assert.Equal(Chosen.StartSample, Intro.Value.Frames);
        Assert.Equal(Chosen.EndSample - Chosen.StartSample, Loop.Value.Frames);
    }

    [Fact]
    public void The_loop_is_at_the_target_level_unless_a_peak_holds_it_lower_and_neither_clip_peaks_above_the_ceiling()
    {
        CutRecord cut = Record.Value.Cut;
        double loopLevel = Loop.Value.LoudRmsDbfs(cut.QuietBelowDbfs);
        double highestPeak = Math.Max(Intro.Value.PeakDbfs, Loop.Value.PeakDbfs);

        Assert.True(highestPeak <= cut.PeakCeilingDbfs + 0.01, $"a clip peaks at {highestPeak:0.00} dBFS, over {cut.PeakCeilingDbfs}");
        bool isAtTheTarget = Math.Abs(loopLevel - cut.TargetRmsDbfs) <= 0.05;
        bool isHeldByAPeak = loopLevel < cut.TargetRmsDbfs && Math.Abs(highestPeak - cut.PeakCeilingDbfs) <= 0.05;
        Assert.True(isAtTheTarget || isHeldByAPeak, $"the loop is at {loopLevel:0.00} dBFS RMS with a peak of {highestPeak:0.00} dBFS");
    }

    [Fact]
    public void The_loop_ends_on_the_sample_the_intro_ends_on_so_it_leads_into_its_own_start_as_the_intro_does()
    {
        short[] introEnd = Intro.Value.FrameAt(Intro.Value.Frames - 1);
        short[] loopEnd = Loop.Value.FrameAt(Loop.Value.Frames - 1);

        Assert.All(Enumerable.Range(0, introEnd.Length), channel => Assert.InRange(loopEnd[channel] - introEnd[channel], -1, 1));
    }

    private static string ClipPath(string file) => RepoFiles.RepositoryFile($"{Record.Value.Cut.Folder}/{file}");

    private sealed record MusicRecord(string Piece, SourceRecord Source, LicenceRecord Licence, CutRecord Cut);

    private sealed record SourceRecord(string File, string Sha256, int SampleRate, int Channels, int BitsPerSample, long Frames);

    private sealed record LicenceRecord(string MadeWith, string Account, string Generated, string CommercialUse, bool Instrumental, string Prompt);

    private sealed record CutRecord(
        string Loop, Dictionary<string, LoopRecord> Loops, double CrossfadeSeconds, double TargetRmsDbfs, double PeakCeilingDbfs, double QuietBelowDbfs,
        string Folder, string IntroFile, string LoopFile);

    private sealed record LoopRecord(long StartSample, long EndSample, int Bars);

    /// <summary>A 16-bit PCM WAV file's format and samples, as the cut tool writes it.</summary>
    private sealed class Wave
    {
        private readonly short[] _samples;

        private Wave(int rate, int channels, int bits, short[] samples)
        {
            Rate = rate;
            Channels = channels;
            Bits = bits;
            _samples = samples;
        }

        public int Rate { get; }

        public int Channels { get; }

        public int Bits { get; }

        public long Frames => _samples.Length / Channels;

        public double PeakDbfs => Decibels(_samples.Max(sample => Math.Abs((int)sample)) / FullScale);

        /// <exception cref="InvalidDataException">The file is not a 16-bit PCM WAV file.</exception>
        public static Wave Read(string path)
        {
            using var reader = new BinaryReader(File.OpenRead(path));
            if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "RIFF" || reader.ReadUInt32() == 0 || Encoding.ASCII.GetString(reader.ReadBytes(4)) != "WAVE")
            {
                throw new InvalidDataException($"{path} is not a WAV file.");
            }

            int rate = 0, channels = 0, bits = 0;
            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                string chunk = Encoding.ASCII.GetString(reader.ReadBytes(4));
                int length = reader.ReadInt32();
                if (chunk == "fmt ")
                {
                    byte[] format = reader.ReadBytes(length);
                    channels = BitConverter.ToInt16(format, 2);
                    rate = BitConverter.ToInt32(format, 4);
                    bits = BitConverter.ToInt16(format, 14);
                }
                else if (chunk == "data" && bits == 16)
                {
                    byte[] data = reader.ReadBytes(length);
                    var samples = new short[length / 2];
                    Buffer.BlockCopy(data, 0, samples, 0, length);
                    return new Wave(rate, channels, bits, samples);
                }
                else
                {
                    reader.BaseStream.Seek(length + (length & 1), SeekOrigin.Current);
                }
            }

            throw new InvalidDataException($"{path} has no 16-bit samples.");
        }

        public short[] FrameAt(long frame) => _samples.AsSpan((int)(frame * Channels), Channels).ToArray();

        /// <summary>The level of the music itself, as the cut tool measures it: the RMS of the mono mix over the samples above the quiet threshold.</summary>
        public double LoudRmsDbfs(double quietBelowDbfs)
        {
            double threshold = Math.Pow(10, quietBelowDbfs / 20);
            double sum = 0;
            long count = 0;
            for (int frame = 0; frame < Frames; frame++)
            {
                double mono = 0;
                for (int channel = 0; channel < Channels; channel++)
                {
                    mono += _samples[frame * Channels + channel] / FullScale;
                }

                mono /= Channels;
                if (Math.Abs(mono) > threshold)
                {
                    sum += mono * mono;
                    count++;
                }
            }

            return Decibels(Math.Sqrt(sum / count));
        }

        private static double Decibels(double amplitude) => 20 * Math.Log10(Math.Max(amplitude, 1e-12));
    }
}
