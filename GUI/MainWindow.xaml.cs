using System;
using System.IO;
using System.IO.Ports;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace SyringePumpController
{
    public partial class MainWindow : Window
    {
        private SerialPort serialPort;
        private bool isHomed = false;
        private bool isFlowRateSet = false;
        private bool isRunning = false;
        private string settingsFilePath = "settings.json";
        private const int MICROSTEPS = 256; // Microsteps setting
        private double fluidAmount;
        private double flowRate;
        private DispatcherTimer timer;
        private DispatcherTimer comPortTimer; // Timer to update COM ports
        private const double FULL_STEPS_PER_MM = 200 * MICROSTEPS / 8;

        public MainWindow()
        {
            InitializeComponent();
            LoadSettings();
            PopulateComPorts();
            buttonHome.IsEnabled = false;
            buttonSetFlowRate.IsEnabled = false;
            buttonStartStop.IsEnabled = false;
            buttonJogForward.IsEnabled = false;
            buttonJogBackward.IsEnabled = false;
            buttonEStop.IsEnabled = false;
            buttonJogSpeed.IsEnabled = false;  

            // Initialize the timer to update the time to empty every second
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += Timer_Tick;

            // Initialize the timer to update the COM ports every 5 seconds
            comPortTimer = new DispatcherTimer();
            comPortTimer.Interval = TimeSpan.FromSeconds(5);
            comPortTimer.Tick += ComPortTimer_Tick;
            comPortTimer.Start();
        }

        /// <summary>
        /// Populates the COM port combo box with available COM ports.
        /// </summary>
        private void PopulateComPorts()
        {
            string[] ports = SerialPort.GetPortNames();
            comboBoxComPorts.Items.Clear(); // Clear existing items

            foreach (string port in ports)
            {
                comboBoxComPorts.Items.Add(port);
            }

            // Re-select the previously selected COM port if still available
            if (comboBoxComPorts.Items.Contains(comboBoxComPorts.SelectedItem))
            {
                comboBoxComPorts.SelectedItem = comboBoxComPorts.SelectedItem;
            }
            else if (comboBoxComPorts.Items.Count > 0)
            {
                comboBoxComPorts.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// Timer tick event for updating the COM ports.
        /// </summary>
        private void ComPortTimer_Tick(object sender, EventArgs e)
        {
            PopulateComPorts();
        }

        /// <summary>
        /// Loads the settings from the JSON file.
        /// </summary>
        private void LoadSettings()
        {
            if (File.Exists(settingsFilePath))
            {
                try
                {
                    string json = File.ReadAllText(settingsFilePath);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        Settings settings = JsonSerializer.Deserialize<Settings>(json);
                        if (settings != null)
                        {
                            textBoxDiameter.Text = settings.Diameter.ToString();
                            textBoxFluidAmount.Text = settings.FluidAmount.ToString();
                            textBoxFlowRate.Text = settings.FlowRate.ToString();
                            numericUpDownJogSpeed.Value = (decimal)settings.JogSpeed;
                            comboBoxComPorts.SelectedItem = settings.ComPort;
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to load settings: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Saves the current settings to the JSON file.
        /// </summary>
        private void SaveSettings()
        {
            try
            {
                Settings settings = new Settings
                {
                    Diameter = string.IsNullOrWhiteSpace(textBoxDiameter.Text) ? 0 : Convert.ToDouble(textBoxDiameter.Text),
                    FluidAmount = string.IsNullOrWhiteSpace(textBoxFluidAmount.Text) ? 0 : Convert.ToDouble(textBoxFluidAmount.Text),
                    FlowRate = string.IsNullOrWhiteSpace(textBoxFlowRate.Text) ? 0 : Convert.ToDouble(textBoxFlowRate.Text),
                    ComPort = comboBoxComPorts.SelectedItem?.ToString(),
                    JogSpeed = (double)numericUpDownJogSpeed.Value // Handle the nullable value
                };

                string json = JsonSerializer.Serialize(settings);
                File.WriteAllText(settingsFilePath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Initializes the connection to the selected COM port.
        /// </summary>
        private void buttonInitialize_Click(object sender, RoutedEventArgs e)
        {
            if (comboBoxComPorts.SelectedItem == null)
            {
                MessageBox.Show("Please select a COM port.");
                return;
            }

            try
            {
                if (serialPort != null && serialPort.IsOpen)
                {
                    serialPort.Close();
                }

                serialPort = new SerialPort(comboBoxComPorts.SelectedItem.ToString(), 115200);
                serialPort.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);
                serialPort.Open();
                SendCommand("ENABLE"); // Enable the motor driver
                textBlockStatus.Text = $"Connected to {comboBoxComPorts.SelectedItem.ToString()}";

                buttonHome.IsEnabled = true;
                buttonEStop.IsEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open COM port: {ex.Message}");
                textBlockStatus.Text = "Failed to connect.";
            }
        }

        /// <summary>
        /// Sends a command to the Arduino via the serial port.
        /// </summary>
        /// <param name="command">The command to send.</param>
        private void SendCommand(string command)
        {
            if (serialPort != null && serialPort.IsOpen)
            {
                serialPort.WriteLine(command);
                textBlockStatus.Text = $"Sent: {command}";
            }
            else
            {
                textBlockStatus.Text = "Serial port not open.";
            }
        }

        /// <summary>
        /// Enables the controls after homing is complete.
        /// </summary>
        private void EnableControls()
        {
            buttonSetFlowRate.IsEnabled = true;
            buttonJogForward.IsEnabled = true;
            buttonJogBackward.IsEnabled = true;
            buttonEStop.IsEnabled = true;
            buttonJogSpeed.IsEnabled = true;
        }

        /// <summary>
        /// Sets the flow rate based on the input values and sends the necessary commands to the Arduino.
        /// </summary>
        private void SendSpeedToArduino(double speedMmPerHr)
        {
            // Convert speed from mm/hr to steps per second
            double speedMmPerSec = speedMmPerHr / 3600.0;
            double speedStepsPerSec = speedMmPerSec * FULL_STEPS_PER_MM;

            // Send the calculated speed to the Arduino
            SendCommand($"SET_SPEED {speedStepsPerSec}");
        }

        // Modify buttonSetFlowRate_Click to send the speed to the Arduino
        private void buttonSetFlowRate_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(textBoxDiameter.Text) ||
                string.IsNullOrEmpty(textBoxFlowRate.Text) ||
                string.IsNullOrEmpty(textBoxFluidAmount.Text))
            {
                MessageBox.Show("Please fill all fields");
                return;
            }

            buttonStartStop.IsEnabled = true;

            // Send the flow rate setting to the Arduino
            string command = $"SET_FLOW_RATE {textBoxDiameter.Text} {textBoxFlowRate.Text}";
            SendCommand(command);
            isFlowRateSet = true;

            // Calculate and display the "Time to Empty"
            double diameter = Convert.ToDouble(textBoxDiameter.Text);
            fluidAmount = Convert.ToDouble(textBoxFluidAmount.Text);
            flowRate = Convert.ToDouble(textBoxFlowRate.Text);

            double radius = diameter / 2.0;
            double area = Math.PI * Math.Pow(radius, 2); // Cross-sectional area in cubic mm

            // Convert flow rate from mL/hr to mm³/hr
            double flowRateMm3PerHr = flowRate * 1000.0; // 1 mL = 1000 mm³

            // Calculate the required speed in mm/hr
            double speedMmPerHr = flowRateMm3PerHr / area;

            // Calculate time to empty
            double timeToEmptyHours = fluidAmount / flowRate; // in hours

            // Display debug information
            Console.WriteLine($"Diameter: {diameter}");
            Console.WriteLine($"Fluid Amount (mL): {fluidAmount}");
            Console.WriteLine($"Flow Rate (mL/hr): {flowRate}");
            Console.WriteLine($"Area (mm²): {area}");
            Console.WriteLine($"Flow Rate (mm³/hr): {flowRateMm3PerHr}");
            Console.WriteLine($"Speed (mm/hr): {speedMmPerHr}");
            Console.WriteLine($"Time to Empty (hours): {timeToEmptyHours}");

            // Display time to empty in hh:mm:ss format
            TimeSpan timeToEmpty = TimeSpan.FromHours(timeToEmptyHours);
            labelTimeToEmpty.Content = timeToEmpty.ToString(@"hh\:mm\:ss");

            // Send the calculated speed to the Arduino
            SendSpeedToArduino(speedMmPerHr);

            buttonStartStop.IsEnabled = true;
        }

        /// <summary>
        /// Starts or stops the syringe pump based on the current state.
        /// </summary>
        private void buttonStartStop_Click(object sender, RoutedEventArgs e)
        {
            if (!isFlowRateSet)
            {
                MessageBox.Show("Set the flow rate first");
                return;
            }

            if (isRunning)
            {
                SendCommand("STOP");
                buttonStartStop.Content = "Start";
                buttonStartStop.Foreground = new SolidColorBrush(Colors.Green);
                timer.Stop();
                isRunning = false;
                buttonJogBackward.IsEnabled = true;
                buttonJogForward.IsEnabled = true;
                buttonHome.IsEnabled = true;
            }
            else
            {
                SendCommand("START");
                buttonStartStop.Content = "Stop";
                buttonStartStop.Foreground = new SolidColorBrush(Colors.Red);
                timer.Start();
                isRunning = true;
                buttonJogBackward.IsEnabled = false;
                buttonJogForward.IsEnabled = false;
                buttonHome.IsEnabled = false;
            }
        }

        /// <summary>
        /// Updates the remaining time to empty every second.
        /// </summary>
        private void Timer_Tick(object sender, EventArgs e)
        {
            if (flowRate > 0 && fluidAmount > 0)
            {
                fluidAmount -= flowRate / 3600.0; // Subtract flow rate per second
                if (fluidAmount <= 0)
                {
                    fluidAmount = 0;
                    timer.Stop();
                    SendCommand("STOP");
                    buttonStartStop.Content = "Start";
                    buttonStartStop.Foreground = new SolidColorBrush(Colors.Green);
                    isRunning = false;
                }

                double timeToEmptyHours = fluidAmount / flowRate; // in hours
                TimeSpan timeToEmpty = TimeSpan.FromHours(timeToEmptyHours);
                labelTimeToEmpty.Content = timeToEmpty.ToString(@"hh\:mm\:ss");
            }
        }

        /// <summary>
        /// Sends the jog forward command to the Arduino when the jog forward button is pressed.
        /// </summary>
        private void buttonJogForward_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (!isHomed)
            {
                MessageBox.Show("Home the motor first");
                return;
            }
            SendCommand("JOG_FORWARD");
        }

        /// <summary>
        /// Stops jogging when the jog forward button is released.
        /// </summary>
        private void buttonJogForward_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            SendCommand("STOP_JOG");
        }

        /// <summary>
        /// Sends the jog backward command to the Arduino when the jog backward button is pressed.
        /// </summary>
        private void buttonJogBackward_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (!isHomed)
            {
                MessageBox.Show("Home the motor first");
                return;
            }
            SendCommand("JOG_BACKWARD");
        }

        /// <summary>
        /// Stops jogging when the jog backward button is released.
        /// </summary>
        private void buttonJogBackward_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            SendCommand("STOP_JOG");
        }

        /// <summary>
        /// Sends command to set position to 0
        /// </summary>
        private void buttonSetPosition_Click(object sender, RoutedEventArgs e)
        {
            SendCommand("SETPOS_0");
        }

        /// <summary>
        /// Sends the home command to the Arduino.
        /// </summary>
        /// 
        private void buttonHome_Click(object sender, RoutedEventArgs e)
        {
            SendCommand("HOME");
        }

        /// <summary>
        /// Sends the emergency stop command to the Arduino.
        /// </summary>
        private void buttonEStop_Click(object sender, RoutedEventArgs e)
        {
            SendCommand("ESTOP");
        }

        /// <summary>
        /// Sends jog speed to Arduino
        /// </summary>
        private void buttonJogSpeed_Click(object sender, RoutedEventArgs e)
        {
            decimal jogSpeed = numericUpDownJogSpeed.Value; // Convert the nullable value to int
            SendCommand($"JOGSPEED {jogSpeed}");
        }

        /// <summary>
        /// Handles data received from the Arduino.
        /// </summary>
        private void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort sp = (SerialPort)sender;
            string data = sp.ReadLine();
            this.Dispatcher.Invoke(() =>
            {
                if (data.StartsWith("Current position in steps: "))
                {
                    labelCurrentPosition.Content = (Convert.ToDouble(data.Substring(26)) / MICROSTEPS / 8).ToString("F2"); // Assuming the message format is "Current position in steps: XX"
                }
                else if (data.StartsWith("Current RPM: "))
                {
                    labelCurrentRPM.Content = data.Substring(12); // Assuming the message format is "Current RPM at gearbox output: XX.XX"
                }
                else
                {
                    textBlockStatus.Text = $"Received: {data}";
                }

                if (data.Contains("Homing complete"))
                {
                    isHomed = true;
                    EnableControls(); // Enable other controls after homing is completed
                }
            });
        }

        /// <summary>
        /// Handles the window closing event. Disables the motor driver and saves settings.
        /// </summary>
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (serialPort != null && serialPort.IsOpen)
            {
                SendCommand("DISABLE");
                serialPort.Close();
            }
            SaveSettings();
        }
    }

    /// <summary>
    /// Class to store settings for the application.
    /// </summary>
    public class Settings
    {
        public double Diameter { get; set; }
        public double FluidAmount { get; set; }
        public double FlowRate { get; set; }
        public string ComPort { get; set; }
        public double JogSpeed { get; set; } // Store jog speed as a double
    }
}
