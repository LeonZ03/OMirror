using System;
namespace OMirror {
    class ScreenPowerPolicyTests {
        static void Check(bool value, string name) { if (!value) throw new Exception(name); }
        static int Main() {
            Check(ScreenPowerPolicy.ReadLocked("KeyguardServiceDelegate\r\n showing=true\r\n mIsShowing=false\r\n") == true, "lock transition must keep screen on");
            Check(ScreenPowerPolicy.ReadLocked("KeyguardServiceDelegate\n showing=false\n inputRestricted=true\n") == true, "restricted input requires light");
            Check(ScreenPowerPolicy.ReadLocked("KeyguardServiceDelegate\n showing=false\n mIsShowing=false\n inputRestricted=false\n") == false, "unlocked");
            Check(ScreenPowerPolicy.ReadLocked("showing=false") == null, "unrelated window is not unlock evidence");
            Check(ScreenPowerPolicy.ReadLocked("KeyguardServiceDelegate\n secure=true\n") == null, "unknown OEM format");
            Check(ScreenPowerPolicy.ReadLocked(null) == null, "failed query");
            Check(ScreenPowerPolicy.ReadLocked("KeyguardServiceDelegate\n inputRestricted=false\n") == null, "restriction report alone is not unlock evidence");
            foreach (bool preferOff in new[] { false, true }) {
                Check(!ScreenPowerPolicy.ShouldTurnOff(preferOff, true), "locked must be lit in either mode");
                Check(!ScreenPowerPolicy.ShouldTurnOff(preferOff, null), "unknown must be lit");
                Check(ScreenPowerPolicy.ShouldTurnOff(preferOff, false) == preferOff, "unlocked follows preference");
            }
            Check(ScreenPowerPolicy.ReadPhysicalPower("Display 1\n connectionType=Internal\n powerMode=Off\n Virtual Display 2\n powerMode=On\n") == false, "virtual display must not mask an off panel");
            Check(ScreenPowerPolicy.ReadPhysicalPower("Display 1\n connectionType=Internal\n powerMode=On\n") == true, "physical on");
            Check(ScreenPowerPolicy.ReadPhysicalPower("Virtual Display 2\n powerMode=On\n") == null, "no physical evidence");
            Check(ScreenPowerPolicy.ReadPhysicalPower("Display 1\n connectionType=Internal\n Virtual Display 2\n powerMode=On\n") == null, "missing physical power must not bleed into next display");
            Console.WriteLine("Screen power policy: 17 checks passed");
            return 0;
        }
    }
}
