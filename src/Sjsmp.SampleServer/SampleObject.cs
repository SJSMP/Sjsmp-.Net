using Sjsmp.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sjmp.SampleServer
{
    internal sealed class SampleObject
    {
        private readonly object m_lock = new object();
        private volatile Thread m_thread;

        public SampleObject()
        {
            intervalSeconds = 1;
        }

        [SjsmpProperty("boolean field")]
        internal bool booleanField { get; set; }
        [SjsmpProperty("timer interval")]
        internal int intervalSeconds { get; set; }
        [SjsmpProperty("value being changed by timer", isReadonly: true, showGraph: true)]
        internal int timedValue { get; private set; }
        [SjsmpProperty("int property with limits"), SjsmpPropertyLimits(-10, 5)]
        internal int intLimits { get; set; }
        [SjsmpProperty("double property with limits"), SjsmpPropertyLimits(-10.0, 5.0)]
        internal double doubleLimits { get; set; }


        private void threadFunc()
        {
            while (true)
            {
                ++timedValue;
                Thread.Sleep(intervalSeconds * 1000);
            }
        }

        [SjsmpAction("Starts timer", requireConfirm:true)]
        public void startTimer()
        {
            lock (m_lock)
            {
                if (m_thread == null)
                {
                    m_thread = new Thread(threadFunc);
                    m_thread.Start();
                }
            }
        }

        [SjsmpAction("Stops timer")]
        public void stopTimer()
        {
            lock (m_lock)
            {
                if (m_thread != null)
                {
                    m_thread.Abort();
                    m_thread = null;
                }
            }
        }

        [SjsmpAction("Returns true if the timer is currently running")]
        public bool isTimerRunning()
        {
            return m_thread != null;
        }

        [SjsmpAction("Returns same value that is passed as paramerter")]
        public string returnSame([SjsmpActionParameter("parameter to be returned")] string param)
        {
            return param;
        }

    }
}
