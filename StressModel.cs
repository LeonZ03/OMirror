using System;

namespace OMirror
{
    internal static class StressModel
    {
        private enum State { Stopped, Starting, Running, Stopping, Recovering }

        public static int Run(int seed, int iterations)
        {
            Random random = new Random(seed);
            State state = State.Stopped;
            int generation = 0;
            int activeGeneration = 0;
            int recoveryAttempts = 0;
            bool stopRequested = false;
            bool saved = true;

            for (int i = 0; i < iterations; i++)
            {
                switch (random.Next(10))
                {
                    case 0: // user starts
                        if (saved && state == State.Stopped)
                        {
                            state = State.Starting;
                            activeGeneration = ++generation;
                            stopRequested = false;
                        }
                        break;
                    case 1: // window becomes ready
                        if (state == State.Starting)
                            state = State.Running;
                        break;
                    case 2: // user stops
                        if (state == State.Starting || state == State.Running || state == State.Recovering)
                        {
                            stopRequested = true;
                            state = State.Stopping;
                        }
                        break;
                    case 3: // process exit, including a deliberately stale callback
                        int callbackGeneration = random.Next(4) == 0 ? activeGeneration - 1 : activeGeneration;
                        if (!saved || callbackGeneration != activeGeneration)
                            break;
                        if (stopRequested || state == State.Stopping)
                        {
                            state = State.Stopped;
                            stopRequested = false;
                        }
                        else if (state == State.Starting || state == State.Running)
                        {
                            if (recoveryAttempts < 2)
                            {
                                recoveryAttempts++;
                                state = State.Recovering;
                            }
                            else
                                state = State.Stopped;
                        }
                        break;
                    case 4: // recovery launch
                        if (saved && state == State.Recovering)
                        {
                            state = State.Starting;
                            activeGeneration = ++generation;
                        }
                        break;
                    case 5: // rapid screen-off/on input; only the last desired state matters
                        break;
                    case 6: // stable period resets the retry budget
                        if (state == State.Running)
                            recoveryAttempts = 0;
                        break;
                    case 7: // expected keyboard-mode restart
                        if (state == State.Running)
                        {
                            stopRequested = true;
                            state = State.Stopping;
                        }
                        break;
                    case 8: // deleting a saved device invalidates every old callback
                        saved = false;
                        activeGeneration = ++generation;
                        recoveryAttempts = 0;
                        stopRequested = false;
                        state = State.Stopped;
                        break;
                    case 9: // the still-connected phone is explicitly added again
                        if (!saved)
                        {
                            saved = true;
                            recoveryAttempts = 0;
                            stopRequested = false;
                            state = State.Stopped;
                        }
                        break;
                }

                if (activeGeneration < 0 || recoveryAttempts > 2)
                    return 1;
                if (state == State.Stopped && stopRequested)
                    return 2;
                if (!saved && state != State.Stopped)
                    return 3;
            }
            return 0;
        }
    }
}
