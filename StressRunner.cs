using System;
using System.Windows.Forms;

namespace OPhoneMirror
{
    // This runner deliberately uses the same public event paths as a person using
    // the panel. It is not a mock: screen-power queuing, hot restarts and process
    // recovery all run in the real application.
    internal sealed class StressRunner
    {
        internal static int LastExitCode;

        private readonly MainForm form;
        private readonly Random random;
        private readonly Timer timer;
        private readonly int deviceIndex;
        private readonly DateTime endUtc;
        private readonly bool originalScreenOff;
        private readonly bool originalTopMost;
        private readonly int originalKeyboard;
        private readonly int originalDevice;
        private int actionNumber;
        private bool startedByRunner;

        private StressRunner(MainForm form, int deviceIndex, int durationMinutes, int seed)
        {
            this.form = form;
            this.deviceIndex = deviceIndex;
            random = new Random(seed);
            endUtc = DateTime.UtcNow.AddMinutes(durationMinutes);
            originalScreenOff = form.StressScreenOffSetting;
            originalTopMost = form.StressTopMostSetting;
            originalKeyboard = form.StressKeyboardSetting;
            originalDevice = form.StressActiveDeviceIndex;
            timer = new Timer();
            timer.Tick += Tick;
        }

        internal static void Start(MainForm form, int deviceIndex, int durationMinutes, int seed)
        {
            LastExitCode = 0;
            StressRunner runner = new StressRunner(form, deviceIndex, durationMinutes, seed);
            runner.Begin();
        }

        private void Begin()
        {
            if (!form.StressPrepare(deviceIndex))
            {
                Fail("target-not-ready");
                return;
            }
            if (form.StressMirrorProcessCount() > 1)
            {
                Fail("duplicate-before-start");
                return;
            }
            if (!form.StressMirrorIsRunning())
            {
                startedByRunner = true;
                form.StressStartOrStopMirror();
            }
            Diagnostics.Trace("stress", "started", "device=" + deviceIndex + " duration=" +
                Math.Max(1, (int)(endUtc - DateTime.UtcNow).TotalMinutes));
            ScheduleNext(false);
        }

        private void Tick(object sender, EventArgs e)
        {
            timer.Stop();
            if (DateTime.UtcNow >= endUtc)
            {
                Finish();
                return;
            }
            if (form.StressMirrorProcessCount() > 1)
            {
                Fail("duplicate-process");
                return;
            }

            actionNumber++;
            int roll = random.Next(100);
            if (roll < 30)
            {
                int clicks = random.Next(2, 6);
                for (int i = 0; i < clicks; i++)
                    form.StressToggleScreenPower();
                Diagnostics.Trace("stress", "screen-burst", "step=" + actionNumber + " count=" + clicks);
            }
            else if (roll < 50)
            {
                form.StressTogglePicker();
                if (random.Next(2) == 0)
                    form.StressClosePicker();
                Diagnostics.Trace("stress", "picker", "step=" + actionNumber);
            }
            else if (roll < 65)
            {
                form.StressStartOrStopMirror();
                Diagnostics.Trace("stress", "mirror-toggle", "step=" + actionNumber);
            }
            else if (roll < 80)
            {
                form.StressToggleKeyboardMode();
                Diagnostics.Trace("stress", "keyboard-toggle", "step=" + actionNumber);
            }
            else if (roll < 90)
            {
                form.StressToggleTopMost();
                Diagnostics.Trace("stress", "topmost-toggle", "step=" + actionNumber);
            }
            else
            {
                // HOME / RECENTS / camera preview only. No shutter, no data edits.
                int[] keys = { 3, 187, 27 };
                form.StressNavigatePhone(keys[random.Next(keys.Length)]);
                Diagnostics.Trace("stress", "navigation", "step=" + actionNumber);
            }
            ScheduleNext(true);
        }

        private void ScheduleNext(bool mixedDelay)
        {
            int interval;
            if (!mixedDelay)
                interval = 1000;
            else
            {
                int bucket = random.Next(100);
                interval = bucket < 35 ? random.Next(100, 401) :
                    bucket < 90 ? random.Next(800, 3001) : random.Next(5000, 10001);
            }
            timer.Interval = interval;
            timer.Start();
        }

        private void Finish()
        {
            try
            {
                // Restoring an explicit "on" state avoids leaving the handset dark.
                form.StressRestore(originalScreenOff, originalTopMost, originalKeyboard, originalDevice);
                if (startedByRunner && form.StressMirrorIsRunning())
                    form.StressStartOrStopMirror();
                Diagnostics.Trace("stress", "passed", "actions=" + actionNumber);
            }
            catch (Exception ex)
            {
                LastExitCode = 1;
                Diagnostics.Trace("stress", "restore-failed", ex.GetType().Name);
            }
            finally
            {
                form.BeginInvoke((MethodInvoker)delegate { form.Close(); });
            }
        }

        private void Fail(string reason)
        {
            LastExitCode = 1;
            Diagnostics.Trace("stress", "failed", "step=" + actionNumber + " reason=" + reason);
            Diagnostics.FlushUnexpectedExit("stress", string.Empty, 1);
            Finish();
        }
    }
}
