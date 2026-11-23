using System;
using System.IO;
using SiteDesignPro.Core.Updates;
using Xunit;

namespace SiteDesignPro.Core.Tests
{
    /// <summary>Automatic updates: the description of the newest version (latest.json) and the comparison of versions.</summary>
    public class UpdateTests
    {
        private const string Sha = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        private static string Json(string version, string sha = Sha) =>
            "{\"version\":\"" + version + "\",\"date\":\"2026-11-23\",\"notes\":[\"Ispravka A\",\"Ispravka B\"]," +
            "\"autocad\":{\"url\":\"https://github.com/amsalamsal/projektni-biro/releases/download/v" + version + "/SiteDesignPro.bundle.zip\",\"sha256\":\"" + sha + "\"}," +
            "\"sketchup\":{\"url\":\"https://github.com/x/y/releases/download/v1/pb_site_design_pro.rbz\",\"sha256\":\"" + Sha.ToUpperInvariant() + "\"}}";

        [Fact]
        public void TheDescriptionOfAVersionIsRead()
        {
            UpdateInfo u = UpdateInfo.Parse(Json("0.3.2"));
            Assert.Equal("0.3.2", u.Version);
            Assert.Equal(new[] { "Ispravka A", "Ispravka B" }, u.Notes);
            Assert.EndsWith("SiteDesignPro.bundle.zip", u.AutoCadUrl);
            Assert.Equal(Sha, u.SketchUpSha256);
        }

        [Fact]
        public void AnIncompleteDescriptionIsRefused()
        {
            Assert.Throws<FormatException>(() => UpdateInfo.Parse("{\"version\":\"0.3.2\"}"));
            Assert.Throws<FormatException>(() => UpdateInfo.Parse(Json("0.3.2", "abc")));
            Assert.Throws<FormatException>(() => UpdateInfo.Parse("<html>nije json</html>"));
        }

        [Theory]
        [InlineData("0.3.2", "0.3.1", true)]
        [InlineData("0.4.0", "0.3.9", true)]
        [InlineData("1.0", "0.9.9", true)]
        [InlineData("v0.10.0", "0.9.0", true)]
        [InlineData("0.3.1", "0.3.1", false)]
        [InlineData("0.3.0", "0.3.1", false)]
        [InlineData("x", "0.3.1", false)]
        public void VersionsAreComparedByNumber(string candidate, string current, bool newer) =>
            Assert.Equal(newer, UpdateInfo.IsNewer(candidate, current));

        [Fact]
        public void TheChecksumOfADownloadIsComputed()
        {
            string f = Path.GetTempFileName();
            File.WriteAllText(f, "abc");
            Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", UpdateInfo.Sha256(f));
            File.Delete(f);
        }
    }
}
