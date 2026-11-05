using System.IO;
using System.Text;
using Puzzle.Bank.Candidates;
using Puzzle.Bank.Measuring;
using Puzzle.Bank.Storage;

namespace Puzzle.Core.Tests;

public class BankStorageTests
{
    private static readonly byte[][] ThreeRecords =
    [
        RecordFrame.Encode(RecordType.Draw, Encoding.UTF8.GetBytes("the first record")),
        RecordFrame.Encode(RecordType.Board, Encoding.UTF8.GetBytes("a second, somewhat longer record")),
        RecordFrame.Encode(RecordType.Draw, Encoding.UTF8.GetBytes("the last record")),
    ];

    private static byte[] Shard => ThreeRecords.SelectMany(frame => frame).ToArray();

    [Fact]
    public void A_whole_shard_gives_back_every_record_in_order()
    {
        ShardScan scan = ShardReader.Scan(Shard, "whole");

        Assert.Equal(["the first record", "a second, somewhat longer record", "the last record"], scan.Records.Select(TextOf));
        Assert.Equal([RecordType.Draw, RecordType.Board, RecordType.Draw], scan.Records.Select(record => record.Type));
        Assert.True(scan.IsSound);
        Assert.Equal(0, scan.TornTailBytes);
    }

    [Fact]
    public void A_shard_cut_at_any_byte_of_its_last_record_keeps_every_earlier_record_and_reports_a_torn_tail()
    {
        byte[] shard = Shard;
        int lastStarts = ThreeRecords[0].Length + ThreeRecords[1].Length;

        for (int length = lastStarts + 1; length < shard.Length; length++)
        {
            ShardScan scan = ShardReader.Scan(shard[..length], "cut");

            Assert.Equal(["the first record", "a second, somewhat longer record"], scan.Records.Select(TextOf));
            Assert.Equal(length - lastStarts, scan.TornTailBytes);
            Assert.True(scan.IsSound, $"a tail of {length - lastStarts} bytes is a cut-off record, not damage");
        }
    }

    [Fact]
    public void A_flipped_byte_costs_the_record_it_lies_in_and_no_other()
    {
        int secondStarts = ThreeRecords[0].Length;
        for (int at = secondStarts; at < secondStarts + ThreeRecords[1].Length; at++)
        {
            byte[] shard = Shard;
            shard[at] ^= 0x40;

            ShardScan scan = ShardReader.Scan(shard, "flipped");

            Assert.Equal(["the first record", "the last record"], scan.Records.Select(TextOf));
            Assert.Equal([new UnreadableRange(secondStarts, ThreeRecords[1].Length)], scan.Unreadable);
        }
    }

    [Fact]
    public void A_damaged_last_record_is_damage_and_not_a_torn_tail()
    {
        byte[] shard = Shard;
        shard[^1] ^= 0x01; // inside the checksum: the record is whole and wrong

        ShardScan scan = ShardReader.Scan(shard, "damaged");

        Assert.Equal(2, scan.Records.Count);
        Assert.False(scan.IsSound);
        Assert.Equal(0, scan.TornTailBytes);
    }

    [Fact]
    public void A_record_of_a_type_this_build_does_not_know_is_read_and_has_no_type()
    {
        byte[] future = RecordFrame.Encode((RecordType)200, Encoding.UTF8.GetBytes("from a newer tool"));

        ShardScan scan = ShardReader.Scan([.. ThreeRecords[0], .. future, .. ThreeRecords[2]], "newer");

        Assert.Equal(3, scan.Records.Count);
        Assert.Null(scan.Records[1].Type);
        Assert.True(scan.IsSound);
    }

    [Fact]
    public void Games_read_back_exactly_as_they_were_written()
    {
        BankCandidate candidate = BankSupport.AsBankCandidate(BankSupport.Drawn(BankSupport.CollectSpec(), seed: 3));
        RungGames written = LadderMeasurer.Measure(BankSupport.BreadthOf(candidate, 40, 85))[1];

        ShardScan scan = ShardReader.Scan(RecordCodec.Frame(written), "games");
        RungGames read = RecordCodec.ReadGames(scan.Records.Single());

        Assert.Equal(written.Header with { SeedIndices = [], ProgressMarks = [] }, read.Header with { SeedIndices = [], ProgressMarks = [] });
        Assert.Equal(written.Header.SeedIndices, read.Header.SeedIndices);
        Assert.Equal(written.Header.ProgressMarks, read.Header.ProgressMarks);
        Assert.Equal(written.Bytes.ToArray(), read.Bytes.ToArray());
        Assert.Equal(BankSupport.Rung(85), read.Header.Player);
    }

    [Fact]
    public void A_bank_read_from_disk_holds_what_was_stored_under_the_ids_it_was_stored_with()
    {
        using var folder = new TemporaryBankFolder();
        BankStore store = BankStore.Create(folder.Path, bankSeed: 5);
        var spec = BankSupport.SpecWith("9x10", 6, new FamilyDraw(ObstacleKind.Boxes, 2));
        BankCandidate stored = new(
            BankSupport.Drawn(spec, seed: 11),
            [7u, 8u, 9u, 10u],
            new AmountBase([], 0),
            amountStep: 0,
            new CandidateOrigin.Drawn(Draw: 4, Variant: 1, Seed: 11, spec, BoardDrawer.DrawCodeVersion),
            new DateTime(2026, 10, 8, 1, 2, 3, DateTimeKind.Utc));
        RungGames[] games = LadderMeasurer.Measure(BankSupport.BreadthOf(stored, 50, 70));
        var draw = new DrawRecord(4, 1, "a class|Boxes", "skill-55", RecordMapping.ToRecord(spec), 11, 0, DrawOutcome.Stored, stored.Content.Id.ToString(), 25, 26, true, 0.5, null);
        using (BankWriter writer = store.OpenWriter(new BankCatalog()))
        {
            writer.Store(stored, games, draw);
        }

        BankScan scan = BankStore.Open(folder.Path).Read();
        BankCandidate read = Assert.Single(scan.Catalog.Candidates);

        Assert.True(scan.IsSound);
        Assert.Equal(stored.Content.Id, read.Content.Id);
        Assert.Equal(stored.Content.Board, read.Content.Board);
        Assert.Equal(CanonicalText.OfCandidate(stored.Content.Level), CanonicalText.OfCandidate(read.Content.Level));
        Assert.Equal(stored.Seeds, read.Seeds);
        Assert.Equal(stored.CreatedUtc, read.CreatedUtc);
        var origin = Assert.IsType<CandidateOrigin.Drawn>(read.Origin);
        Assert.Equal((4L, 1, 11UL, BoardDrawer.DrawCodeVersion), (origin.Draw, origin.Variant, origin.Seed, origin.DrawCode));
        Assert.Equal(spec.Families, origin.Spec.Families);
        Assert.Equal(games.Select(batch => batch.Finishes()), read.Games.Select(batch => batch.Finishes));
        Assert.Equal(draw, Assert.Single(scan.Catalog.Draws) with { Spec = draw.Spec });
        Assert.Equal(5, scan.Catalog.NextDrawIndex);
        Assert.Equal(games[0].Bytes.ToArray(), store.ReadGames(read.Games[0].Location!).Bytes.ToArray());
    }

    [Fact]
    public void A_retracted_candidate_is_no_longer_read_though_its_records_stay()
    {
        using var folder = new TemporaryBankFolder();
        BankStore store = BankStore.Create(folder.Path, bankSeed: 5);
        BankCandidate kept = BankSupport.AsBankCandidate(BankSupport.Drawn(BankSupport.CollectSpec(), seed: 1));
        BankCandidate withdrawn = BankSupport.AsBankCandidate(BankSupport.Drawn(BankSupport.CollectSpec(), seed: 2));
        var catalog = new BankCatalog();
        using (BankWriter writer = store.OpenWriter(catalog))
        {
            foreach (BankCandidate candidate in new[] { kept, withdrawn })
            {
                writer.Store(candidate, LadderMeasurer.Measure(BankSupport.BreadthOf(candidate, 55)), DrawFor(candidate));
            }

            writer.Retract([withdrawn.Content.Id], "drawn by a faulty build");
        }

        BankScan scan = store.Read();

        Assert.Equal([kept.Content.Id], catalog.Candidates.Select(candidate => candidate.Content.Id));
        Assert.Equal([kept.Content.Id], scan.Catalog.Candidates.Select(candidate => candidate.Content.Id));
        Assert.Equal(1, scan.Catalog.RetractedCount);
        Assert.True(scan.IsSound);
    }

    [Fact]
    public void A_bank_that_needs_a_newer_reader_is_refused_and_not_read_in_part()
    {
        using var folder = new TemporaryBankFolder();
        BankStore.Create(folder.Path, bankSeed: 5);
        string manifest = Path.Combine(folder.Path, "bank.json");
        File.WriteAllText(manifest, File.ReadAllText(manifest).Replace("\"minReaderVersion\": 1", $"\"minReaderVersion\": {BankFormat.ReaderVersion + 1}"));

        var refused = Assert.Throws<BankFormatException>(() => BankStore.Open(folder.Path));

        Assert.Contains("needs a reader of version", refused.Message);
    }

    [Fact]
    public void A_lost_manifest_is_written_again_from_the_shards()
    {
        using var folder = new TemporaryBankFolder();
        BankStore created = BankStore.Create(folder.Path, bankSeed: 77);
        using (BankWriter writer = created.OpenWriter(new BankCatalog()))
        {
            writer.Open(new EpochRecord("0011223344556677", "a fingerprint", 0, DateTime.UtcNow, "runtime", "core", "bot", "a test"));
        }

        File.Delete(Path.Combine(folder.Path, "bank.json"));
        BankStore reopened = BankStore.Open(folder.Path);

        Assert.Equal(created.Identity, reopened.Identity);
        Assert.True(File.Exists(Path.Combine(folder.Path, "bank.json")));
    }

    [Fact]
    public void A_folder_without_a_bank_is_not_opened_as_one()
    {
        using var folder = new TemporaryBankFolder();
        Directory.CreateDirectory(folder.Path);

        Assert.False(BankStore.Exists(folder.Path));
        Assert.Throws<BankFormatException>(() => BankStore.Open(folder.Path));
    }

    [Fact]
    public void A_second_grower_is_refused_while_the_first_holds_the_bank_and_admitted_afterwards()
    {
        using var folder = new TemporaryBankFolder();
        BankStore store = BankStore.Create(folder.Path, bankSeed: 5);

        using (store.LockForGrowth())
        {
            var busy = Assert.Throws<BankBusyException>(() => store.LockForGrowth());
            Assert.Contains($"process {Environment.ProcessId}", busy.Message);
        }

        using GrowLock afterwards = store.LockForGrowth(); // the lock file is still there; nobody holds it
        Assert.NotNull(afterwards);
    }

    [Fact]
    public void A_shard_of_another_bank_is_not_mixed_in()
    {
        using var folder = new TemporaryBankFolder();
        using var otherFolder = new TemporaryBankFolder();
        BankStore store = BankStore.Create(folder.Path, bankSeed: 5);
        BankStore other = BankStore.Create(otherFolder.Path, bankSeed: 6);
        using (BankWriter writer = other.OpenWriter(new BankCatalog()))
        {
            writer.Open(new EpochRecord("0011223344556677", "a fingerprint", 0, DateTime.UtcNow, "runtime", "core", "bot", "a test"));
        }

        Directory.CreateDirectory(folder.ShardFolder);
        File.Copy(otherFolder.OnlyShard, Path.Combine(folder.ShardFolder, Path.GetFileName(otherFolder.OnlyShard)));

        var refused = Assert.Throws<BankFormatException>(() => store.Read());

        Assert.Contains("belongs to bank", refused.Message);
    }

    private static DrawRecord DrawFor(BankCandidate candidate) => new(
        0, 0, "a class|collect only", "skill-55", RecordMapping.ToRecord(BankSupport.CollectSpec()), 1, 0, DrawOutcome.Stored, candidate.Content.Id.ToString(), null, null, false, 0, null);

    private static string TextOf(StoredRecord record) => Encoding.UTF8.GetString(record.Payload.Span);
}
