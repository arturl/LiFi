using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections;

namespace Flasher
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        readonly bool[] prefix_signature = new bool[] { false, false, false, false, true, true, true, true };
        readonly string end_of_file = "\x1A";
        // all time values in milliseconds:
        const int frame_length = 250;
        const int delimeter_length = 250;

        async Task ShowBit(bool b)
        {
            this.Grid.Background = new SolidColorBrush(Windows.UI.Colors.Black);
            await Task.Delay(b ? frame_length*2 : frame_length);
        }

        async Task ShowDelimeter()
        {
            this.Grid.Background = new SolidColorBrush(Windows.UI.Colors.White);
            await Task.Delay(delimeter_length);
        }

        async Task SendSignal(BitArray signal)
        {
            for (int i=0; i<signal.Length; ++i)
            {
                await ShowBit(signal[i]);
                await ShowDelimeter();
            }
        }

        async Task Calibrate()
        {
            for (int i = 0; i < 2; ++i)
            {
                await ShowBit(true);
            }

            for (int i = 0; i < 2; ++i)
            {
                await ShowDelimeter();
            }

        }

        async Task Start()
        {
            BitArray bits = new BitArray(prefix_signature);
            await SendSignal(bits);
        }

        async Task EmitData(string str)
        {
            var startTime = DateTime.Now;

            await Calibrate();

            await Start();

            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(str+ end_of_file);
            BitArray bits = new BitArray(bytes);

            await SendSignal(bits);

            var endTime = DateTime.Now;
            var elapsed = endTime - startTime;
            Debug.WriteLine("Elapsed {0}", elapsed);

            this.Grid.Background = new SolidColorBrush(Windows.UI.Colors.White);
            this.Status.Text = "Ready";
        }

        public MainPage()
        {
            this.InitializeComponent();
            this.Status.Text = "Ready";
        }

        private void button_Click(object sender, RoutedEventArgs e)
        {
            this.Status.Text = "Sending data...";
            EmitData("Hello");
        }
    }
}
