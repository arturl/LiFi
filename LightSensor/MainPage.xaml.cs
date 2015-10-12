using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections;
using System.Threading.Tasks;
using Windows.Devices.Gpio;
using Windows.UI.Xaml.Controls;

namespace LightSensor
{
    public sealed partial class MainPage : Page
    {
        // Constants:
        readonly bool[] prefix_signature = new bool[] { false, false, false, false, true, true, true, true };
        readonly string end_of_file = "\x1A";
        // all time values in milliseconds:
        const int sampling_frequency = 10;
        const int frame_length = 250;
        const int delimeter_length = 250;

        List<int> records = new List<int>();
        int min_value = 99999;
        int max_value = 0;

        int low_threshold_max;
        int high_threshold_min;

        string data = "";

        int RCTime(GpioPin pin, int max)
        {
            pin.SetDriveMode(GpioPinDriveMode.Output);
            pin.Write(GpioPinValue.Low);

            pin.SetDriveMode(GpioPinDriveMode.Input);
            int reading = 0;
            while (pin.Read() == GpioPinValue.Low)
            {
                reading++;
                if (reading >= max) break;
            }
            return reading;
        }

        bool HasSuffix(List<int> data, bool[] signature)
        {
            if (data.Count < signature.Length) return false;

            for (int i = 0; i < signature.Length; ++i)
            {
                int j = data.Count - signature.Length + i;
                if ((data[j]==1) != signature[i])
                    return false;
            }
            return true;
        }

        async Task ReadLight()
        {
            var gpio = GpioController.GetDefault();

            var pin = gpio.OpenPin(13);

            int current_signal_level = -1;
            DateTime lastStateStart = DateTime.Now;

            int data_begin = 0;

            DateTime lastFlashStart = DateTime.Now;

            while (true)
            {
                var now = DateTime.Now;

                // Set the shortcut only after we have started reading data
                int max = int.MaxValue; //  data_begin == 0 ? int.MaxValue : high_threshold_min;
                var beforeRCTime = DateTime.Now;
                var val = RCTime(pin, max);
                var afterRCTime = DateTime.Now;

                Debug.WriteLine(string.Format("RCTime took {0} ms", (afterRCTime - beforeRCTime).TotalMilliseconds));

                if (val < min_value) min_value = val;
                if (val > max_value) max_value = val;

                low_threshold_max = (int)(max_value * 0.49);
                high_threshold_min = (int)(max_value * 0.5);

                int signal_level = -1;
                if (val < low_threshold_max)
                {
                    signal_level = 0;
                }
                else if (val >= high_threshold_min)
                {
                    signal_level = 1;
                }

                var s = string.Format("{0:HH:mm:ss:FFFF},[<{1}]-[>{2}] {3},{4} ", 
                    now, low_threshold_max,
                         high_threshold_min,
                         val, signal_level);
                Debug.Write(s);

                if (signal_level == -1)
                {
                    Debug.WriteLine("ignoring noise");
                    continue; // noise, ignore it
                }

                if (current_signal_level == -1)
                {
                    current_signal_level = signal_level;
                }

                bool got_new_bit = false;
                int this_bit_value = -1;
                if (current_signal_level == 1) // has been dark
                {
                    if (signal_level == 0) // first flash!
                    {
                        var timeDiff = now - lastStateStart;
                        Debug.WriteLine(string.Format("...flash! the value lasted {0} ms", timeDiff.TotalMilliseconds));

                        if (timeDiff < TimeSpan.FromMilliseconds(delimeter_length / 2))
                        {
                            Debug.WriteLine(string.Format("...flash value ignored"));
                            signal_level = 1;
                        }
                        else
                        {
                            if (timeDiff > TimeSpan.FromMilliseconds(frame_length * 1.5))
                            {
                                this_bit_value = 1;
                            }
                            else
                            {
                                this_bit_value = 0;
                            }
                            lastFlashStart = now;
                            got_new_bit = true;
                        }
                    }
                    else // still dark
                    {
                        // no change, ignore
                        Debug.WriteLine("...");
                    }
                }
                else
                {
                    // flash still on
                    if (signal_level == 0)
                    {
                        // flash continues, ignore
                        Debug.WriteLine("...flashing");
                    }
                    else
                    {
                        var timeDiff = now - lastFlashStart;
                        Debug.WriteLine(string.Format("...flash ended after {0} ms, new signal starts", timeDiff.TotalMilliseconds));
                        // first signal after flash
                        lastStateStart = now;
                    }
                }
                current_signal_level = signal_level;

                if (got_new_bit)
                {
                    Debug.WriteLine("*** record[{0}]={1}", records.Count, this_bit_value);

                    records.Add(this_bit_value);

                    if (HasSuffix(records, prefix_signature))
                    {
                        Debug.WriteLine("+++ Found begin of transmission!");
                        data_begin = records.Count;
                    }

                    if (data_begin > 0)
                    {
                        if (records.Count - data_begin == 8)
                        {
                            var bits = new BitArray(8, false);
                            for (int i = 0; i < bits.Length; ++i)
                            {
                                bits[i] = (records[i + data_begin]==1);
                            }

                            data_begin += 8;

                            byte[] byteArray = new byte[1];
                            ((ICollection)bits).CopyTo(byteArray, 0);
                            string block = System.Text.Encoding.UTF8.GetString(byteArray);
                            Debug.WriteLine(string.Format("--> block='{0}', {1}", block, (int)byteArray[0]));

                            if (block == end_of_file)
                            {
                                Debug.WriteLine("Found end of transmission!");
                                break;
                            }

                            data += block;
                        }
                    }
                }

                await Task.Delay(sampling_frequency);
            }
        }

        public MainPage()
        {
            this.InitializeComponent();
            ReadLight();
        }
    }
}
