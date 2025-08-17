using System;
using System.Threading;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Tests.DesktopApp.UI.TestInfra
{
    [CollectionDefinition("UIAutomation", DisableParallelization = true)]
    public class UiAutomationCollection : ICollectionFixture<UiAutomationFixture> { }

    public class UiAutomationFixture { }

    public static class Sta
    {
        public static void Run(Action action)
        {
            Exception? captured = null;
            var t = new Thread(() =>
            {
                try { action(); }
                catch (Exception ex) { captured = ex; }
            });
            t.SetApartmentState(ApartmentState.STA);
            t.IsBackground = true;
            t.Start();
            t.Join();
            if (captured != null) throw captured;
        }
    }
}
