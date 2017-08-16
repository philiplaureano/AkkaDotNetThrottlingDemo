using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;

namespace ThrottlingDemo
{
    public static class ConstantExtensions
    {
        public static double Kilobytes(this int value) => value * 1024f;
        public static double Megabytes(this int value) => value.Kilobytes() * 1024f;
        public static double Gigabytes(this int value) => value.Megabytes() * 1024f;
        public static double Terabytes(this int value) => value.Gigabytes() * 1024f;
        public static double BytesToKilobytes(this long bytes) => bytes / 1024f;
        public static double BytesToMegabytes(this long bytes) => bytes.BytesToKilobytes() / 1024f;
        public static double BytesToGigabytes(this long bytes) => bytes.BytesToMegabytes() / 1024f;
        public static double BytesToTerabytes(this long bytes) => bytes.BytesToGigabytes() / 1024f;
        public static double BytesToKilobytes(this ulong bytes) => bytes / 1024f;
        public static double BytesToMegabytes(this ulong bytes) => bytes.BytesToKilobytes() / 1024f;
        public static double BytesToGigabytes(this ulong bytes) => bytes.BytesToMegabytes() / 1024f;
        public static double BytesToTerabytes(this ulong bytes) => bytes.BytesToGigabytes() / 1024f;
    }
    public class CheckMemoryUsage
    {
    }

    public class MemoryUsageMetric
    {
        public MemoryUsageMetric(double value, DateTime dateSampled)
        {
            Value = value;
            DateSampled = dateSampled;
        }

        public double Value { get; }
        public DateTime DateSampled { get; }
    }

    public class MemoryUsagePollingActor : ReceiveActor
    {
        private readonly TimeSpan _pollingInterval;

        public MemoryUsagePollingActor(TimeSpan pollingInterval)
        {
            _pollingInterval = pollingInterval;

            Receive<CheckMemoryUsage>(message =>
            {
                // Publish the memory usage 
                // to the event stream
                Context.System.EventStream.Publish(
                    new MemoryUsageMetric(GetMemoryBytesUsed(),
                        DateTime.UtcNow));
            });
        }

        protected override void PreStart()
        {
            Context.System.Scheduler.ScheduleTellRepeatedly(TimeSpan.Zero, _pollingInterval, Self,
                new CheckMemoryUsage(), Self);
        }

        private static double GetMemoryBytesUsed()
        {
            using (var process = Process.GetCurrentProcess())
            {
                return process.PrivateMemorySize64;
            }
        }
    }


    public class MemoryUsageAlarm : ReceiveActor
    {
        public MemoryUsageAlarm(double highValue, double mediumValue, double lowValue)
        {
            Receive<MemoryUsageMetric>(message =>
            {
                var bytesUsed = message.Value;
                var megabytes = bytesUsed / 1.Megabytes();
                if (bytesUsed <= lowValue)
                {
                    Console.WriteLine($"Memory Usage: Low ({megabytes:N}MB)");
                }

                if (bytesUsed >= mediumValue)
                {
                    Console.WriteLine($"Memory Usage: Medium ({megabytes:N}MB)");
                }

                if (bytesUsed >= highValue)
                {
                    Console.WriteLine($"Memory Usage: High ({megabytes:N}MB)");
                }
            });
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var system = ActorSystem.Create("MyActorSystem");

            // Start polling 
            var memoryActor = system.ActorOf(Props.Create(() =>
                new MemoryUsagePollingActor(TimeSpan.FromSeconds(1))));

            // Print the memory bytes used
            var printer = system.ActorOf(Props.Create(() => 
                new MemoryUsageAlarm(5.Gigabytes(), 2.Gigabytes(), 1.Gigabytes())));

            system.EventStream.Subscribe(printer, typeof(MemoryUsageMetric));

            Console.WriteLine("Press ENTER to continue...");
            Console.ReadLine();
        }
    }
}
