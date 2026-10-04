using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BiomeRivals.Demo.Editor;
using NUnit.Framework;
using UnityEditor.Build;
using UnityEngine;

namespace BiomeRivals.Demo.Tests
{
    public sealed class DemoCardArtPackagingTests
    {
        private bool _enabled;
        private readonly List<string> _temporaryDirectories = new List<string>();
        private static string TemporaryRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "CardArtPackagingTests"));

        [SetUp] public void Setup() { _enabled = DemoCardArtProvider.LocalArtEnabled; DemoCardArtProvider.LocalArtEnabled = true; DemoCardArtProvider.ClearCache(); }
        [TearDown] public void Teardown()
        {
            DemoCardArtProvider.ClearCache(); DemoCardArtProvider.LocalArtEnabled = _enabled;
            foreach (var path in _temporaryDirectories)
            {
                if (!Path.GetFullPath(path).StartsWith(TemporaryRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Unsafe generated test fixture cleanup path.");
                Directory.Delete(path, true);
            }
            _temporaryDirectories.Clear();
        }

        [TestCase(null)] [TestCase("")] [TestCase("../pf_001")] [TestCase("pf_001/../pf_002")]
        [TestCase("PF_001")] [TestCase("pf_001.png")] [TestCase("pf_001\n")] [TestCase("C:\\pf_001")]
        public void UnsafeIdsNeverBecomeFilesystemReads(string id)
        {
            Assert.That(DemoCardArtProvider.IsSafeCardId(id), Is.False);
            Assert.That(DemoCardArtProvider.Load(id), Is.Null);
            Assert.Throws<ArgumentException>(() => DemoCardArtProvider.GetPackagedIconPath(Application.dataPath, id));
        }

        [Test] public void UnknownSafeIdUsesNormalFallbackWithoutLookingOutsideRegisteredNames()
        { Assert.That(DemoCardArtProvider.IsSafeCardId("zz_999"), Is.True); Assert.That(DemoCardArtProvider.Load("zz_999"), Is.Null); }

        [Test] public void CachedSpritesKeepPixelContractAndRespectDisableSwitch()
        {
            var icon = DemoCardArtProvider.Load("pf_001");
            Assert.That(icon, Is.Not.Null);
            Assert.That(icon.texture.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(icon.texture.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(icon.pixelsPerUnit, Is.EqualTo(16));
            Assert.That(icon.texture.GetPixels32().Any(pixel => pixel.a == 0), Is.True);
            Assert.That(DemoCardArtProvider.Load("pf_001"), Is.SameAs(icon));
            DemoCardArtProvider.LocalArtEnabled = false;
            Assert.That(DemoCardArtProvider.Load("pf_001"), Is.Null, "Cached icons must not bypass the art switch.");
            DemoCardArtProvider.LocalArtEnabled = true;
            Assert.That(DemoCardArtProvider.Load("pf_001"), Is.SameAs(icon));
            DemoCardArtProvider.ClearCache();
            Assert.That(icon == null, Is.True, "Reset must release owned sprites/textures, not only dictionary references.");
            Assert.That(DemoCardArtProvider.Load("pf_001"), Is.Not.Null);
        }

        [Test] public void PackagedPathIsBasedOnPlayerDataDirectoryNotWorkingDirectory()
        {
            var data = Path.Combine(TemporaryRoot, "ExamplePlayer_Data");
            Assert.That(DemoCardArtProvider.GetPackagedIconPath(data, "ed_008"),
                Is.EqualTo(Path.Combine(TemporaryRoot, "MinecraftCardIcons", "ed_008.png")));
            Assert.Throws<ArgumentException>(() => DemoCardArtProvider.GetPackagedIconPath("relative_Data", "ed_008"));
        }

        [Test] public void RealRegisteredArtPassesBuildPreflight()
        {
            var result = Validate(Path.Combine(Application.dataPath, "Generated", "MinecraftCardIcons"));
            Assert.That(result.Length, Is.EqualTo(74));
        }

        [TestCase("missing-provenance", "provenance is missing")]
        [TestCase("missing-icon", "card art is missing")]
        [TestCase("changed-icon", "hash mismatch")]
        [TestCase("unsafe-output", "unknown/duplicate/unsafe")]
        public void BuildPreflightRejectsInvalidGeneratedFixtures(string scenario, string expectedError)
        {
            var directory = Path.Combine(TemporaryRoot, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory); _temporaryDirectories.Add(directory);
            var source = Path.Combine(Application.dataPath, "Generated", "MinecraftCardIcons");
            if (scenario != "missing-provenance")
                File.Copy(Path.Combine(source, "asset-provenance.local.json"), Path.Combine(directory, "asset-provenance.local.json"));
            if (scenario == "changed-icon") File.WriteAllBytes(Path.Combine(directory, "pf_001.png"), new byte[] { 1, 2, 3 });
            if (scenario == "unsafe-output")
                File.WriteAllText(Path.Combine(directory, "asset-provenance.local.json"),
                    "{\"schemaVersion\":1,\"redistributionPolicy\":\"DO_NOT_COMMIT_EXTRACTED_ASSETS\",\"entries\":[{\"cardId\":\"pf_001\",\"outputFile\":\"../pf_001.png\",\"sha256\":\"" + new string('a', 64) + "\"}]}");
            var error = Assert.Throws<TargetInvocationException>(() => Validate(directory));
            Assert.That(error.InnerException, Is.TypeOf<BuildFailedException>());
            Assert.That(error.InnerException.Message, Does.Contain(expectedError));
        }

        private static Array Validate(string directory) => (Array)typeof(DemoBuildAutomation)
            .GetMethod("ValidateCardArtSource", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { directory });
    }
}
