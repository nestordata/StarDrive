using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ship_Game.GameScreens.MainMenu;

namespace UnitTests.UI;

[TestClass]
public class AutoPatcherDesktopVkTests
{
    [TestMethod]
    [DataRow("Content/Globals.yaml", true)]
    [DataRow("Content\\Races\\Human.yaml", true)]
    [DataRow("Mods/CombinedArms/ModInfo.yaml", true)]
    [DataRow("Credits.txt", true)]
    [DataRow("upgrade-url.txt", true)]
    [DataRow("Content/Effects/Vulkan/Simple.mgfx", true)]
    [DataRow("Content/Video/Loading 2.mp4", true)]
    [DataRow("Content/Video/Loading 2.wmv", false)]
    [DataRow("Content/Effects/Simple.mgfx", false)]
    [DataRow("Content/Effects/Simple.fx", false)]
    [DataRow("Content/3DParticles/foo.mgfx", false)]
    [DataRow("StarDrive.dll", false)]
    [DataRow("StarDrive.runtimeconfig.json", false)]
    [DataRow("SDNative.dll", false)]
    [DataRow("StarDrive.exe", false)]
    [DataRow("Content/../StarDrive.dll", false)]
    [DataRow("Mods/../StarDrive.dll", false)]
    public void IsDesktopVkSafePatchPath_filters_windows_zip(string rel, bool expected)
    {
        Assert.AreEqual(expected, AutoPatcher.IsDesktopVkSafePatchPath(rel));
    }

    [TestMethod]
    [DataRow("Content/old.yaml", true)]
    [DataRow("Mods/X/gone.txt", true)]
    [DataRow("StarDrive.exe", false)]
    [DataRow("StarDrive.runtimeconfig.json", false)]
    [DataRow("Content/../StarDrive.dll", false)]
    [DataRow("Mods/../StarDrive.exe", false)]
    public void IsDesktopVkSafeDeletePath_only_content_mods(string rel, bool expected)
    {
        Assert.AreEqual(expected, AutoPatcher.IsDesktopVkSafeDeletePath(rel));
    }

    [TestMethod]
    public void MergeAssemblyVersionWithAppliedContent_prefers_higher_stamp()
    {
        Assert.AreEqual(
            "1.60.00047 jupiter-1.60",
            AutoPatcher.MergeAssemblyVersionWithAppliedContent("1.60.00000 jupiter-1.60", "1.60.00047"));
        Assert.AreEqual(
            "1.60.00050 jupiter-1.60",
            AutoPatcher.MergeAssemblyVersionWithAppliedContent("1.60.00050 jupiter-1.60", "1.60.00047"));
        Assert.AreEqual(
            "1.60.00000 jupiter-1.60",
            AutoPatcher.MergeAssemblyVersionWithAppliedContent("1.60.00000 jupiter-1.60", null));
    }
}
