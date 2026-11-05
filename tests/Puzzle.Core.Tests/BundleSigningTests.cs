using Puzzle.Client.EditorTools;

namespace Puzzle.Core.Tests;

/// <summary>
/// The key the Google Play bundle is signed with (2026-10-28): the owner's upload key only when the keystore is there
/// and both passwords are set, Unity's debug key otherwise, saying which was missing; and neither says a password in
/// what it logs. <c>BundleSigning</c> is the Unity project's Editor source file, compiled into these tests; the bundle
/// build chooses with it (unity/Assets/Editor/PlayBundle.cs).
/// </summary>
public class BundleSigningTests
{
    private const string Keystore = BundleSigning.DefaultKeystorePath;
    private const string StorePassword = "store-secret-123";
    private const string KeyPassword = "key-secret-456";

    [Fact]
    public void With_the_keystore_and_both_passwords_the_bundle_is_signed_with_the_upload_key()
    {
        BundleSigning signing = BundleSigning.Choose(Keystore, keystoreIsThere: true, StorePassword, KeyPassword);

        BundleSigning.UploadKey key = Assert.IsType<BundleSigning.UploadKey>(signing);
        Assert.Equal((Keystore, StorePassword, KeyPassword), (key.KeystorePath, key.StorePassword, key.KeyPassword));
        Assert.Equal("upload", key.FileWord);
        Assert.DoesNotContain(StorePassword, key.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(KeyPassword, key.ToString(), StringComparison.Ordinal);
        Assert.Contains(Keystore, key.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false, StorePassword, KeyPassword, "no keystore at D:/puzzle-cache/keys/museumpuzzle-upload.keystore")]
    [InlineData(true, null, KeyPassword, "PUZZLE_UPLOAD_STORE_PASS is not set")]
    [InlineData(true, "", KeyPassword, "PUZZLE_UPLOAD_STORE_PASS is not set")]
    [InlineData(true, StorePassword, null, "PUZZLE_UPLOAD_KEY_PASS is not set")]
    [InlineData(false, null, null, "no keystore at D:/puzzle-cache/keys/museumpuzzle-upload.keystore; PUZZLE_UPLOAD_STORE_PASS is not set; PUZZLE_UPLOAD_KEY_PASS is not set")]
    public void Without_one_of_the_three_the_bundle_is_signed_with_the_debug_key_and_says_which(bool keystoreIsThere, string? storePassword, string? keyPassword, string why)
    {
        BundleSigning signing = BundleSigning.Choose(Keystore, keystoreIsThere, storePassword, keyPassword);

        BundleSigning.DebugKey key = Assert.IsType<BundleSigning.DebugKey>(signing);
        Assert.Equal(why, key.Why);
        Assert.Equal("debug", key.FileWord);
        Assert.DoesNotContain(StorePassword, key.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(KeyPassword, key.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("META-INF/MANIFEST.MF", true)]
    [InlineData("META-INF/ANDROIDD.SF", true)]
    [InlineData("META-INF/ANDROIDD.RSA", true)]
    [InlineData("META-INF/UPLOAD.SF", true)]
    [InlineData("META-INF/UPLOAD.EC", true)]
    [InlineData("META-INF/CERT.DSA", true)]
    [InlineData("META-INF/upload.sf", true)]
    [InlineData("META-INF/androidx.core_core.version", false)]
    [InlineData("META-INF/services/javax.annotation.processing.Processor", false)]
    [InlineData("META-INF/sub/UPLOAD.SF", false)]
    [InlineData("META-INF/.SF", false)]
    [InlineData("base/manifest/AndroidManifest.xml", false)]
    [InlineData("BUNDLE-METADATA/com.android.tools/d8.json", false)]
    public void The_signature_files_are_the_manifest_and_a_signers_files_directly_under_meta_inf(string entry, bool isSignature)
    {
        Assert.Equal(isSignature, BundleSigning.IsSignatureFile(entry));
    }

    [Fact]
    public void The_upload_keys_signature_is_named_after_its_alias_and_counts_as_a_signature()
    {
        Assert.All(BundleSigning.UploadSignatureFiles, file => Assert.True(BundleSigning.IsSignatureFile(file)));
        Assert.Equal(BundleSigning.UploadSignatureFiles.OrderBy(file => file, StringComparer.Ordinal), BundleSigning.UploadSignatureFiles);
        Assert.Contains($"META-INF/{BundleSigning.Alias.ToUpperInvariant()}.SF", BundleSigning.UploadSignatureFiles);
    }

    [Fact]
    public void A_keystore_has_a_path()
    {
        Assert.Throws<ArgumentException>(() => BundleSigning.Choose(" ", keystoreIsThere: true, StorePassword, KeyPassword));
    }
}
