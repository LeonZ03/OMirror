using System;
using System.Text.RegularExpressions;

namespace OMirror
{
    internal static class ScreenPowerPolicy
    {
        // Fail open (keep the panel on) if an OEM's lock report is unavailable.
        internal static bool? ReadLocked(string policyDump)
        {
            if (string.IsNullOrEmpty(policyDump)) return null;
            int section = policyDump.IndexOf("KeyguardServiceDelegate", StringComparison.Ordinal);
            if (section < 0) return null;
            string keyguard = policyDump.Substring(section);
            MatchCollection reports = Regex.Matches(keyguard,
                @"^\s*(?:showing|mIsShowing|inputRestricted|mInputRestricted)=(true|false)\s*$",
                RegexOptions.Multiline);
            if (reports.Count == 0) return null;
            foreach (Match report in reports)
                if (report.Groups[1].Value == "true") return true;
            // inputRestricted=false alone does not prove the keyguard is gone.
            return Regex.IsMatch(keyguard, @"^\s*(?:showing|mIsShowing)=false\s*$",
                RegexOptions.Multiline) ? (bool?)false : null;
        }

        internal static bool ShouldTurnOff(bool preferOff, bool? locked)
        {
            return preferOff && locked == false;
        }

        internal static bool? ReadPhysicalPower(string dump)
        {
            bool display = false, internalDisplay = false;
            foreach (string raw in (dump ?? string.Empty).Split('\n'))
            {
                string line = raw.Trim();
                if (Regex.IsMatch(line, @"^(?:Virtual )?Display \d+"))
                {
                    display = Regex.IsMatch(line, @"^Display \d+$");
                    internalDisplay = false;
                }
                if (!display) continue;
                if (line == "connectionType=Internal") internalDisplay = true;
                if (!internalDisplay) continue;
                if (line == "powerMode=On") return true;
                if (line == "powerMode=Off") return false;
            }
            return null;
        }
    }
}
